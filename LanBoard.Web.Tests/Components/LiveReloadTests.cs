using LanBoard.Application.Notifications;
using LanBoard.Web.Components;
using Microsoft.Extensions.Logging;

namespace LanBoard.Web.Tests.Components;

public class LiveReloadTests
{
    private readonly FakeNotifier _notifier = new();
    private readonly ListLogger _logger = new();
    private readonly Queue<TaskCompletionSource> _reloadGates = new();
    private int _reloads;
    private int _running;
    private bool _overlapped;

    private LiveReload CreateSut() => new(_notifier, f => f(), ReloadAsync, _logger);

    private async Task ReloadAsync()
    {
        _reloads++;
        await Track(_reloadGates.TryDequeue(out var gate) ? gate.Task : Task.CompletedTask);
    }

    private async Task Track(Task work)
    {
        if (++_running > 1) _overlapped = true;
        try { await work; }
        finally { _running--; }
    }

    [Fact]
    public async Task WithoutStart_DoesNotSubscribe()
    {
        await using var sut = CreateSut();

        await sut.RunAsync(() => Task.CompletedTask);
        _notifier.Raise();

        Assert.Equal(0, _notifier.SubscriberCount);
        Assert.Equal(0, _reloads);
    }

    [Fact]
    public async Task Start_Subscribes_DisposeUnsubscribes()
    {
        var sut = CreateSut();

        sut.Start();
        Assert.Equal(1, _notifier.SubscriberCount);

        await sut.DisposeAsync();
        Assert.Equal(0, _notifier.SubscriberCount);
    }

    [Fact]
    public async Task OnChange_WhenIdle_ReloadsOnce()
    {
        await using var sut = CreateSut();
        sut.Start();

        _notifier.Raise();

        Assert.Equal(1, _reloads);
    }

    [Fact]
    public async Task OnChange_DuringWork_CoalescesIntoOneReloadAfterwards()
    {
        await using var sut = CreateSut();
        sut.Start();
        var gate = new TaskCompletionSource();

        var work = sut.RunAsync(() => Track(gate.Task));
        _notifier.Raise();
        _notifier.Raise();
        _notifier.Raise();
        Assert.Equal(0, _reloads);

        gate.SetResult();
        await work;

        Assert.Equal(1, _reloads);
        Assert.False(_overlapped);
    }

    [Fact]
    public async Task RunAsync_DuringReload_WaitsForIt()
    {
        await using var sut = CreateSut();
        sut.Start();
        var reloadGate = new TaskCompletionSource();
        _reloadGates.Enqueue(reloadGate);
        _notifier.Raise();

        var ran = false;
        var work = sut.RunAsync(() => { ran = true; return Track(Task.CompletedTask); });
        Assert.False(ran);

        reloadGate.SetResult();
        await work;

        Assert.True(ran);
        Assert.False(_overlapped);
    }

    [Fact]
    public async Task DisposeAsync_WaitsForInFlightWork()
    {
        var sut = CreateSut();
        var gate = new TaskCompletionSource();
        _ = sut.RunAsync(() => gate.Task);

        var dispose = sut.DisposeAsync().AsTask();
        Assert.False(dispose.IsCompleted);

        gate.SetResult();
        await dispose;
    }

    [Fact]
    public async Task AfterDispose_NoReloadAndRunAsyncIsNoOp()
    {
        var sut = CreateSut();
        sut.Start();
        await sut.DisposeAsync();

        _notifier.Raise();
        var ran = false;
        await sut.RunAsync(() => { ran = true; return Task.CompletedTask; });

        Assert.Equal(0, _reloads);
        Assert.False(ran);
    }

    [Fact]
    public async Task OnChange_DuringWork_NoReloadIfDisposedMeanwhile()
    {
        var sut = CreateSut();
        sut.Start();
        var gate = new TaskCompletionSource();
        _ = sut.RunAsync(() => gate.Task);
        _notifier.Raise();

        var dispose = sut.DisposeAsync().AsTask();
        gate.SetResult();
        await dispose;

        Assert.Equal(0, _reloads);
    }

    [Fact]
    public async Task ReloadFailure_LoggedAsError_AndNextNotificationStillReloads()
    {
        var fail = true;
        await using var sut = new LiveReload(_notifier, f => f(), () =>
        {
            _reloads++;
            return fail ? Task.FromException(new InvalidOperationException("db down")) : Task.CompletedTask;
        }, _logger);
        sut.Start();

        _notifier.Raise();
        fail = false;
        _notifier.Raise();

        Assert.Equal(2, _reloads);
        Assert.Single(_logger.Entries, e => e.Level == LogLevel.Error && e.Exception is InvalidOperationException);
    }

    [Theory]
    [InlineData(typeof(OperationCanceledException))]
    [InlineData(typeof(ObjectDisposedException))]
    public async Task ExpectedCancellationAfterDispose_NotLoggedAsError(Type exceptionType)
    {
        var gate = new TaskCompletionSource();
        var sut = new LiveReload(_notifier, f => f(), async () =>
        {
            await gate.Task;
            throw exceptionType == typeof(ObjectDisposedException)
                ? new ObjectDisposedException("AppDbContext")
                : new OperationCanceledException();
        }, _logger);
        sut.Start();
        _notifier.Raise();

        var dispose = sut.DisposeAsync().AsTask();
        gate.SetResult();
        await dispose;

        Assert.DoesNotContain(_logger.Entries, e => e.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task RunAsync_WorkFailure_PropagatesToCaller()
    {
        await using var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RunAsync(() => throw new InvalidOperationException()));
    }

    private sealed class FakeNotifier : ILanBoardNotifier
    {
        private Action? _onChange;

        public int SubscriberCount => _onChange?.GetInvocationList().Length ?? 0;
        public IReadOnlyCollection<Guid> OnlineUserIds => [];

        public event Action? OnChange
        {
            add => _onChange += value;
            remove => _onChange -= value;
        }

        public void Raise() => _onChange?.Invoke();
        public bool SetUserOnline(Guid userId) => false;
        public void SetUserOffline(Guid userId) { }
        public void NotifyChanged() => Raise();
    }

    private sealed class ListLogger : ILogger
    {
        public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, exception));
    }
}
