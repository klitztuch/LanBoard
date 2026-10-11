using LanBoard.Application.Authorization;
using LanBoard.Web.Auth;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace LanBoard.Web.Components;

/// <summary>Base for pages calling mutating services: provides the acting user id and inline error handling.</summary>
public abstract class ServicePageBase : ComponentBase
{
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = null!;

    protected string? Error { get; set; }

    protected async Task<Guid> CurrentUserIdAsync()
    {
        var state = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        return state.User.GetUserId() ?? Guid.Empty;
    }

    /// <summary>Runs a service call, showing permission and business-rule failures inline instead of crashing the circuit.</summary>
    protected async Task<bool> RunAsync(Func<Task> action)
    {
        Error = null;
        try
        {
            await action();
            return true;
        }
        catch (PermissionDeniedException)
        {
            Error = "Keine Berechtigung.";
        }
        catch (InvalidOperationException ex)
        {
            Error = ex.Message;
        }
        return false;
    }

    /// <summary>Like <see cref="RunAsync(Func{Task})"/>, returning the call's result or null on failure.</summary>
    protected async Task<T?> RunAsync<T>(Func<Task<T>> action) where T : class
    {
        T? result = null;
        // Statement lambda: an expression-bodied one would return T and recurse into this overload.
        await RunAsync(async () => { result = await action(); });
        return result;
    }
}
