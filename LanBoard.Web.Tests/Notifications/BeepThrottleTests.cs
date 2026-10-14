using LanBoard.Web.Notifications;
using Microsoft.Extensions.Time.Testing;

namespace LanBoard.Web.Tests.Notifications;

public class BeepThrottleTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly BeepThrottle _sut;

    public BeepThrottleTests() => _sut = new BeepThrottle(_time, TimeSpan.FromSeconds(1));

    [Fact]
    public void Burst_BeepsOnce()
    {
        Assert.True(_sut.TryBeep());
        Assert.False(_sut.TryBeep());
        _time.Advance(TimeSpan.FromMilliseconds(999));
        Assert.False(_sut.TryBeep());
    }

    [Fact]
    public void AfterInterval_BeepsAgain()
    {
        _sut.TryBeep();
        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.True(_sut.TryBeep());
    }
}
