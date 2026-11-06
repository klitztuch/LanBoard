using LanBoard.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LanBoard.Web.Tests.Services;

public class PerCallTests
{
    private readonly List<FakeService> _created = [];
    private readonly PerCall<IFakeService> _sut;

    public PerCallTests()
    {
        var services = new ServiceCollection();
        services.AddScoped<IFakeService>(_ =>
        {
            var service = new FakeService();
            _created.Add(service);
            return service;
        });
        services.AddTransient(typeof(PerCall<>));
        _sut = services.BuildServiceProvider(validateScopes: true).GetRequiredService<PerCall<IFakeService>>();
    }

    [Fact]
    public async Task SequentialCalls_GetDistinctInstances_BothDisposed()
    {
        IFakeService? first = null, second = null;

        await _sut.Run(s => { first = s; return Task.CompletedTask; });
        await _sut.Run(s => { second = s; return Task.CompletedTask; });

        Assert.NotSame(first, second);
        Assert.Equal(2, _created.Count);
        Assert.All(_created, s => Assert.True(s.Disposed));
    }

    [Fact]
    public async Task RunWithResult_ReturnsResult_DisposesScope()
    {
        var result = await _sut.Run(s => Task.FromResult(42));

        Assert.Equal(42, result);
        Assert.True(Assert.Single(_created).Disposed);
    }

    [Fact]
    public async Task CallThrows_Propagates_DisposesScope()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.Run(_ => Task.FromException(new InvalidOperationException())));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.Run<int>(_ => Task.FromException<int>(new InvalidOperationException())));

        Assert.Equal(2, _created.Count);
        Assert.All(_created, s => Assert.True(s.Disposed));
    }

    [Fact]
    public async Task OverlappingCalls_GetDistinctInstances_EachDisposedAfterItsOwnCall()
    {
        var firstGate = new TaskCompletionSource();
        var secondGate = new TaskCompletionSource<int>();

        var first = _sut.Run(_ => firstGate.Task);
        var second = _sut.Run(_ => secondGate.Task);

        Assert.Equal(2, _created.Count);
        Assert.NotSame(_created[0], _created[1]);
        Assert.All(_created, s => Assert.False(s.Disposed));

        secondGate.SetResult(7);
        Assert.Equal(7, await second);
        Assert.False(_created[0].Disposed);
        Assert.True(_created[1].Disposed);

        firstGate.SetResult();
        await first;
        Assert.True(_created[0].Disposed);
    }

    public interface IFakeService;

    private sealed class FakeService : IFakeService, IAsyncDisposable
    {
        public bool Disposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
