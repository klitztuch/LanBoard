namespace LanBoard.Web.Notifications;

/// <summary>Allows one beep per interval, so a burst of toasts plays the sound once.</summary>
public sealed class BeepThrottle(TimeProvider time, TimeSpan interval)
{
    private DateTimeOffset? _last;

    public bool TryBeep()
    {
        var now = time.GetUtcNow();
        if (_last is { } last && now - last < interval)
            return false;
        _last = now;
        return true;
    }
}
