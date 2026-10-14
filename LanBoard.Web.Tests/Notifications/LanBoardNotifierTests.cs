using LanBoard.Application.Notifications;
using LanBoard.Web.Services;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace LanBoard.Web.Tests.Notifications;

public class LanBoardNotifierTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly LanBoardNotifier _sut;
    private readonly Guid _user = Guid.NewGuid();

    public LanBoardNotifierTests()
        => _sut = new LanBoardNotifier(_time, Options.Create(new NotificationOptions { OnlineGracePeriodSeconds = 60 }));

    [Fact]
    public void FirstLogin_CameOnline()
    {
        Assert.True(_sut.SetUserOnline(_user));
        Assert.Contains(_user, _sut.OnlineUserIds);
    }

    [Fact]
    public void SecondTab_NotCameOnline()
    {
        _sut.SetUserOnline(_user);
        Assert.False(_sut.SetUserOnline(_user));
    }

    [Fact]
    public void Reload_NewCircuitBeforeOldDisposes_NotCameOnline()
    {
        _sut.SetUserOnline(_user);
        Assert.False(_sut.SetUserOnline(_user));
        _sut.SetUserOffline(_user);
        Assert.Contains(_user, _sut.OnlineUserIds);
    }

    [Fact]
    public void Reload_OldCircuitDisposesFirst_WithinGrace_NotCameOnline()
    {
        _sut.SetUserOnline(_user);
        _sut.SetUserOffline(_user);
        _time.Advance(TimeSpan.FromSeconds(59));
        Assert.False(_sut.SetUserOnline(_user));
    }

    [Fact]
    public void PrerenderFlap_OnlyFirstCameOnline()
    {
        Assert.True(_sut.SetUserOnline(_user));
        _sut.SetUserOffline(_user);
        _time.Advance(TimeSpan.FromMilliseconds(50));
        Assert.False(_sut.SetUserOnline(_user));
    }

    [Fact]
    public void BackAfterTwoMinutes_CameOnline()
    {
        _sut.SetUserOnline(_user);
        _sut.SetUserOffline(_user);
        Assert.DoesNotContain(_user, _sut.OnlineUserIds);
        _time.Advance(TimeSpan.FromMinutes(2));
        Assert.True(_sut.SetUserOnline(_user));
    }

    [Fact]
    public void OfflineWithoutOnline_IsIgnored()
    {
        _sut.SetUserOffline(_user);
        Assert.Empty(_sut.OnlineUserIds);
        Assert.True(_sut.SetUserOnline(_user));
    }

    [Fact]
    public void ChangesRaiseOnChange()
    {
        var raised = 0;
        _sut.OnChange += () => raised++;
        _sut.SetUserOnline(_user);
        _sut.SetUserOffline(_user);
        Assert.Equal(2, raised);
    }

    [Fact]
    public async Task ParallelOnlineOffline_EndsConsistent()
    {
        var other = Guid.NewGuid();
        _sut.SetUserOnline(other);

        await Task.WhenAll(Enumerable.Range(0, 1000).Select(_ => Task.Run(() =>
        {
            _sut.SetUserOnline(_user);
            _sut.SetUserOffline(_user);
        })));

        Assert.Equal([other], _sut.OnlineUserIds);
        _sut.SetUserOnline(_user);
        _sut.SetUserOffline(_user);
        Assert.Equal([other], _sut.OnlineUserIds);
    }
}
