using Bunit;
using LanBoard.Web.Components;
using LanBoard.Web.Tests.Localization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace LanBoard.Web.Tests.Components;

public class ServicePageBaseTests : BunitContext
{
    private readonly ListLogger _logger = new();
    private readonly string _generic;

    public ServicePageBaseTests()
    {
        TestLocalizer.UseCulture("en");
        var localizer = TestLocalizer.Create();
        _generic = localizer["Error.Generic"];
        Services.AddSingleton<IStringLocalizer<SharedResource>>(localizer);
        Services.AddSingleton<ILogger<ServicePageBase>>(_logger);
        AddAuthorization();
    }

    private static Task Click(IRenderedComponent<ServicePageBaseProbe> cut, string id)
        => cut.Find($"#{id}").ClickAsync(new MouseEventArgs());

    [Fact]
    public async Task OverlappingClicks_SecondHandlerStartsAfterFirstCompletes()
    {
        var cut = Render<ServicePageBaseProbe>();
        var first = new TaskCompletionSource();
        var second = new TaskCompletionSource();
        cut.Instance.Gates.Enqueue(first);
        cut.Instance.Gates.Enqueue(second);

        var click1 = Click(cut, "slow");
        var click2 = Click(cut, "slow");
        Assert.Equal(["start 1"], cut.Instance.Calls);

        first.SetResult();
        await click1;
        cut.WaitForAssertion(() => Assert.Equal(["start 1", "end 1", "start 2"], cut.Instance.Calls));

        second.SetResult();
        await click2;
        Assert.Equal(["start 1", "end 1", "start 2", "end 2"], cut.Instance.Calls);
    }

    [Fact]
    public async Task IsBusy_WhileHandlerAwaits_ClearedAfterSuccess()
    {
        var cut = Render<ServicePageBaseProbe>();
        var gate = new TaskCompletionSource();
        cut.Instance.Gates.Enqueue(gate);

        var click = Click(cut, "slow");
        Assert.True(cut.Instance.Busy);
        Assert.True(cut.Instance.RenderedBusy[^1]);

        gate.SetResult();
        await click;
        Assert.False(cut.Instance.Busy);
        cut.WaitForAssertion(() => Assert.False(cut.Instance.RenderedBusy[^1]));
    }

    [Fact]
    public async Task IsBusy_ClearedAfterFailure()
    {
        var cut = Render<ServicePageBaseProbe>();
        var gate = new TaskCompletionSource();
        cut.Instance.Gates.Enqueue(gate);

        var click = Click(cut, "slow");
        Assert.True(cut.Instance.Busy);

        gate.SetException(new InvalidOperationException());
        await click;
        Assert.False(cut.Instance.Busy);
        cut.WaitForAssertion(() => Assert.False(cut.Instance.RenderedBusy[^1]));
    }

    [Fact]
    public async Task QueuedClick_KeepsBusyUntilItCompletes()
    {
        var cut = Render<ServicePageBaseProbe>();
        var first = new TaskCompletionSource();
        var second = new TaskCompletionSource();
        cut.Instance.Gates.Enqueue(first);
        cut.Instance.Gates.Enqueue(second);
        var click1 = Click(cut, "slow");
        var click2 = Click(cut, "slow");
        var rendersBefore = cut.Instance.RenderedBusy.Count;

        first.SetResult();
        await click1;
        cut.WaitForAssertion(() => Assert.Contains("start 2", cut.Instance.Calls));
        Assert.True(cut.Instance.Busy);
        second.SetResult();
        await click2;

        var renders = cut.Instance.RenderedBusy.Skip(rendersBefore).ToList();
        Assert.All(renders.SkipLast(1), Assert.True);
        Assert.False(renders[^1]);
    }

    [Fact]
    public async Task SynchronousHandler_NeverSetsBusy()
    {
        var cut = Render<ServicePageBaseProbe>();

        await Click(cut, "sync");

        Assert.Equal(1, cut.Instance.SyncClicks);
        Assert.DoesNotContain(true, cut.Instance.RenderedBusy);
    }

    [Fact]
    public void SlowInitialLoad_NeverSetsBusy()
    {
        var gate = new TaskCompletionSource();
        var cut = Render<ServicePageBaseProbe>(p => p.Add(x => x.OnInit, () => gate.Task));
        Assert.False(cut.Instance.Busy);

        gate.SetResult();

        cut.WaitForAssertion(() => Assert.True(cut.Instance.RenderedBusy.Count >= 2));
        Assert.DoesNotContain(true, cut.Instance.RenderedBusy);
    }

    [Fact]
    public async Task UnexpectedExceptionInHandler_ShowsGenericError_LogsError()
    {
        var cut = Render<ServicePageBaseProbe>();

        await Click(cut, "throw");

        Assert.Equal(_generic, cut.Instance.ErrorMessage);
        cut.WaitForAssertion(() => Assert.Equal(_generic, cut.Find("#error").TextContent));
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Error && e.Exception is InvalidOperationException);
    }

    [Fact]
    public void UnexpectedExceptionInOnInitializedAsync_ShowsGenericError_LogsError()
    {
        var cut = Render<ServicePageBaseProbe>(p => p.Add(x => x.OnInit, async () =>
        {
            await Task.Yield();
            throw new InvalidOperationException("load failed");
        }));

        cut.WaitForAssertion(() => Assert.Equal(_generic, cut.Find("#error").TextContent));
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Error && e.Exception is InvalidOperationException);
    }

    [Fact]
    public async Task DomainException_KeepsSpecificMessage_NoErrorLog()
    {
        var cut = Render<ServicePageBaseProbe>();

        await Click(cut, "domain");

        Assert.NotNull(cut.Instance.ErrorMessage);
        Assert.NotEqual(_generic, cut.Instance.ErrorMessage);
        Assert.DoesNotContain(_logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task Dispose_SkipsQueuedHandler_LateExceptionLoggedAtDebugOnly()
    {
        var cut = Render<ServicePageBaseProbe>();
        var first = new TaskCompletionSource();
        cut.Instance.Gates.Enqueue(first);
        cut.Instance.Gates.Enqueue(new TaskCompletionSource());
        var probe = cut.Instance;
        var click1 = Click(cut, "slow");
        var click2 = Click(cut, "slow");

        await DisposeComponentsAsync();
        first.SetException(new InvalidOperationException("late"));
        await click1;
        await click2;

        Assert.Equal(["start 1"], probe.Calls);
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Debug && e.Exception is InvalidOperationException);
        Assert.DoesNotContain(_logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task ChildAwaitingParentEventCallback_Completes()
    {
        var cut = Render<ServicePageBaseProbe>(p => p.Add(x => x.WithChild, true));
        var child = cut.FindComponents<ServicePageBaseProbe>().Single(c => c.Instance != cut.Instance);

        await child.Find("#save").ClickAsync(new MouseEventArgs()).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(child.Instance.SaveCompleted);
        Assert.True(cut.Instance.ParentSaved);
    }

    private sealed class ListLogger : ILogger<ServicePageBase>
    {
        public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, exception));
    }
}
