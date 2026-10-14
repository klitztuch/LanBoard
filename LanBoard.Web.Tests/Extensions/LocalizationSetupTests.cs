using LanBoard.Web.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LanBoard.Web.Tests.Extensions;

public class LocalizationSetupTests
{
    [Fact]
    public void ConfiguredCultures_AreUsedExactlyOnce()
    {
        var options = Build(new()
        {
            ["Localization:DefaultCulture"] = "en",
            ["Localization:SupportedCultures:0"] = "en",
            ["Localization:SupportedCultures:1"] = "de",
        });

        Assert.Equal(["en", "de"], options.SupportedUICultures!.Select(c => c.Name));
        Assert.Equal(["en", "de"], options.SupportedCultures!.Select(c => c.Name));
        Assert.Equal("en", options.DefaultRequestCulture.UICulture.Name);
    }

    [Fact]
    public void CookieWinsOverBrowser_AndQueryStringIsIgnored()
    {
        var options = Build(new() { ["Localization:SupportedCultures:0"] = "en" });

        Assert.Collection(options.RequestCultureProviders,
            p => Assert.IsType<CookieRequestCultureProvider>(p),
            p => Assert.IsType<AcceptLanguageHeaderRequestCultureProvider>(p));
    }

    private static RequestLocalizationOptions Build(Dictionary<string, string?> config)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(config);
        builder.AddLanBoardLocalization();
        return builder.Services.BuildServiceProvider().GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;
    }
}
