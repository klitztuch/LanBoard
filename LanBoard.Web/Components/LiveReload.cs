using LanBoard.Application.Notifications;

namespace LanBoard.Web.Components;

/// <summary>
/// Owns a component's live-update subscription and all of its DB work, so the work is serialised and
/// awaited on dispose: the circuit's DI scope (and its <c>DbContext</c>) is then never disposed while a
/// query is still running. Notifications that arrive during work coalesce into one reload afterwards.
/// All members except the <see cref="ILanBoardNotifier.OnChange"/> handler run on the Blazor dispatcher.
/// </summary>
public sealed class LiveReload(
    ILanBoardNotifier notifier,
    Func<Func<Task>, Task> invokeAsync,
    Func<Task> reloadAsync,
    ILogger logger) : IAsyncDisposable
{
    private readonly string _component = reloadAsync.Target?.GetType().Name ?? "component";
    private Task? _current;
    private bool _pending;
    private bool _started;
    private volatile bool _disposed;

    /// <summary>Subscribes to live updates. Call only when <c>RendererInfo.IsInteractive</c>: prerendered instances must stay read-once.</summary>
    public void Start()
    {
        if (_started || _disposed) return;
        _started = true;
        notifier.OnChange += OnChange;
    }

    /// <summary>
    /// Runs DB work (the initial load, a reload after a mutation) serialised with live reloads and awaited by
    /// <see cref="DisposeAsync"/>. Exceptions of <paramref name="work"/> propagate to the caller. No-op after dispose.
    /// </summary>
    public Task RunAsync(Func<Task> work)
    {
        if (_disposed) return Task.CompletedTask;
        var task = ChainAsync(_current, work);
        _current = task;
        return task;
    }

    /// <summary>Unsubscribes, stops further reloads and waits for the in-flight work.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (_started) notifier.OnChange -= OnChange;
        if (_current is not { } current) return;
        try
        {
            await current;
        }
        catch
        {
            // Already logged (reloads) or surfaced to the RunAsync caller.
        }
    }

    private void OnChange()
    {
        if (_disposed) return;
        _ = ScheduleReloadAsync();
    }

    private async Task ScheduleReloadAsync()
    {
        try
        {
            await invokeAsync(() =>
            {
                if (_disposed) return Task.CompletedTask;
                _pending = true;
                if (_current is null || _current.IsCompleted)
                    _current = DrainPendingAsync();
                return Task.CompletedTask;
            });
        }
        catch (Exception ex)
        {
            LogFailure(ex);
        }
    }

    private async Task ChainAsync(Task? previous, Func<Task> work)
    {
        if (previous is not null)
        {
            try
            {
                await previous;
            }
            catch
            {
                // Handled by whoever started it.
            }
        }
        if (_disposed) return;
        try
        {
            await work();
        }
        finally
        {
            await DrainPendingAsync();
        }
    }

    private async Task DrainPendingAsync()
    {
        while (_pending && !_disposed)
        {
            _pending = false;
            try
            {
                await reloadAsync();
            }
            catch (Exception ex)
            {
                LogFailure(ex);
            }
        }
    }

    private void LogFailure(Exception ex)
    {
        if (_disposed && ex is OperationCanceledException or ObjectDisposedException)
            logger.LogDebug(ex, "Live reload of {Component} ended by disposal", _component);
        else
            logger.LogError(ex, "Live reload of {Component} failed", _component);
    }
}
