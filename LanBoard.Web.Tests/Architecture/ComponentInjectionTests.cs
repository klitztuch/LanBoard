using System.Reflection;
using LanBoard.Application.Admin;
using LanBoard.Infrastructure;
using LanBoard.Web.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Hosting;

namespace LanBoard.Web.Tests.Architecture;

/// <summary>
/// Components must reach DB_Services through <c>PerCall&lt;T&gt;</c>: a directly injected service shares the
/// circuit's DbContext between overlapping operations (issue #45).
/// </summary>
public class ComponentInjectionTests
{
    private static readonly IReadOnlySet<Type> DbServices = CollectDbServices();

    [Fact]
    public void DbServiceSet_IsNotEmpty()
    {
        Assert.Contains(typeof(IAdminService), DbServices);
    }

    [Fact]
    public void Components_DoNotInjectDbServicesDirectly()
    {
        var violations = typeof(ServicePageBase).Assembly.GetTypes()
            .Where(t => typeof(IComponent).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(p => p.IsDefined(typeof(InjectAttribute)) && DbServices.Contains(p.PropertyType))
                .Select(p => $"{t.Name}.{p.Name} : {p.PropertyType.Name}"))
            .Order()
            .ToList();

        Assert.True(violations.Count == 0,
            "Inject PerCall<T> instead of these DB services:\n" + string.Join("\n", violations));
    }

    /// <summary>Application interfaces whose registered implementation lives in Infrastructure.</summary>
    private static HashSet<Type> CollectDbServices()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration["ConnectionStrings:postgres"] = "Host=localhost";
        builder.AddInfrastructure();

        var application = typeof(IAdminService).Assembly;
        var infrastructure = typeof(InfrastructureExtensions).Assembly;
        return builder.Services
            .Where(d => d.ServiceType.IsInterface && d.ServiceType.Assembly == application
                && d.ImplementationType?.Assembly == infrastructure)
            .Select(d => d.ServiceType)
            .ToHashSet();
    }
}
