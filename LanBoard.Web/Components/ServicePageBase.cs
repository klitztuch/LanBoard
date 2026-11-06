using LanBoard.Application.Authorization;
using LanBoard.Application.Errors;
using LanBoard.Web.Auth;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;

namespace LanBoard.Web.Components;

/// <summary>
/// Base for pages calling mutating services: provides the acting user id, inline error handling,
/// in-order handling of the page's own user events with a busy state, and containment of unexpected errors.
/// </summary>
/// <remarks>
/// User events of one page run one after another. A handler must not await an <see cref="EventCallback"/>
/// that targets the same component: it would wait for its own event lock and deadlock.
/// </remarks>
public abstract class ServicePageBase : ComponentBase, IHandleEvent, IAsyncDisposable
{
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = null!;
    [Inject] private IStringLocalizer<SharedResource> Localizer { get; set; } = null!;
    [Inject] protected ILogger<ServicePageBase> Logger { get; set; } = null!;

    private readonly SemaphoreSlim _eventLock = new(1, 1);
    private int _queuedEvents;
    private bool _disposed;

    protected string? Error { get; set; }

    /// <summary>True while a user-event handler of this page is queued or awaiting async work. UX only, not a lock.</summary>
    protected bool IsBusy { get; private set; }

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

    public override async Task SetParametersAsync(ParameterView parameters)
    {
        // A ParameterView is only valid synchronously: apply it before the first await.
        parameters.SetParameterProperties(this);
        try
        {
            await base.SetParametersAsync(ParameterView.Empty);
        }
        catch (Exception ex)
        {
            Contain(ex);
        }
    }

    async Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem item, object? arg)
    {
        // The renderer's sync context is single-threaded: nothing can take the lock between check and wait.
        if (_eventLock.CurrentCount == 0)
        {
            _queuedEvents++;
            IsBusy = true;
            StateHasChanged();
            await _eventLock.WaitAsync();
            _queuedEvents--;
        }
        else
        {
            await _eventLock.WaitAsync();
        }

        try
        {
            if (_disposed)
                return;

            var task = item.InvokeAsync(arg);
            if (!task.IsCompleted)
            {
                IsBusy = true;
                StateHasChanged();
            }
            await task;
        }
        catch (Exception ex)
        {
            Contain(ex);
        }
        finally
        {
            IsBusy = _queuedEvents > 0;
            _eventLock.Release();
            if (!_disposed)
                StateHasChanged();
        }
    }

    public ValueTask DisposeAsync()
    {
        // In-flight handlers finish on their own operation scope; only their renders are skipped.
        _disposed = true;
        GC.SuppressFinalize(this);
        return DisposeAsyncCore();
    }

    protected virtual ValueTask DisposeAsyncCore() => ValueTask.CompletedTask;

    private void Contain(Exception ex)
    {
        if (_disposed)
        {
            Logger.LogDebug(ex, "Page operation failed after the page was disposed.");
            return;
        }

        Logger.LogError(ex, "Unexpected error in page operation.");
        Error = Localizer["Error.Generic"];
        StateHasChanged();
    }
}
