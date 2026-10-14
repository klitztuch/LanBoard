using LanBoard.Application.Authorization;
using LanBoard.Application.Errors;
using LanBoard.Web.Auth;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;

namespace LanBoard.Web.Components;

/// <summary>Base for pages calling mutating services: provides the acting user id and inline error handling.</summary>
public abstract class ServicePageBase : ComponentBase
{
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Localizer { get; set; } = null!;
    [Inject] protected ILogger<ServicePageBase> Logger { get; set; } = null!;

    protected string? Error { get; set; }

    /// <summary>Field errors of the last <see cref="ValidationException"/>; empty after a successful call.</summary>
    protected IReadOnlyList<ValidationError> ValidationErrors { get; private set; } = [];

    protected async Task<Guid> CurrentUserIdAsync()
    {
        var state = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        return state.User.GetUserId() ?? Guid.Empty;
    }

    /// <summary>Runs a service call, showing permission and business-rule failures inline instead of crashing the circuit.</summary>
    protected async Task<bool> RunAsync(Func<Task> action)
    {
        Error = null;
        ValidationErrors = [];
        try
        {
            await action();
            return true;
        }
        catch (PermissionDeniedException ex)
        {
            Error = ErrorMessages.For(Localizer, ex);
        }
        catch (BusinessRuleException ex)
        {
            Logger.LogInformation(ex, "Business rule violated: {ErrorCode}", ex.Code);
            Error = ErrorMessages.For(Localizer, ex);
        }
        catch (ValidationException ex)
        {
            ValidationErrors = ex.Errors;
            Error = ErrorMessages.For(Localizer, ex);
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
