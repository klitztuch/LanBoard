using LanBoard.Application.Notifications;
using Microsoft.Extensions.Options;

namespace LanBoard.Web.Services;

public class LanBoardNotifier(TimeProvider time, IOptions<NotificationOptions> options) : ILanBoardNotifier
{
    private readonly Lock _lock = new();
    private readonly Dictionary<Guid, int> _connections = [];
    private readonly Dictionary<Guid, DateTimeOffset> _lastOffline = [];
    private readonly TimeSpan _gracePeriod = TimeSpan.FromSeconds(options.Value.OnlineGracePeriodSeconds);

    public IReadOnlyCollection<Guid> OnlineUserIds
    {
        get
        {
            lock (_lock)
                return [.. _connections.Keys];
        }
    }

    public event Action? OnChange;

    public bool SetUserOnline(Guid userId)
    {
        bool cameOnline;
        lock (_lock)
        {
            // Decided from the state before this connection is counted.
            var count = _connections.GetValueOrDefault(userId);
            _connections[userId] = count + 1;
            cameOnline = count == 0
                && (!_lastOffline.TryGetValue(userId, out var lastOffline) || time.GetUtcNow() - lastOffline >= _gracePeriod);
        }
        OnChange?.Invoke();
        return cameOnline;
    }

    public void SetUserOffline(Guid userId)
    {
        lock (_lock)
        {
            if (!_connections.TryGetValue(userId, out var count)) return;
            if (count > 1)
            {
                _connections[userId] = count - 1;
            }
            else
            {
                _connections.Remove(userId);
                _lastOffline[userId] = time.GetUtcNow();
            }
        }
        OnChange?.Invoke();
    }

    public void NotifyChanged() => OnChange?.Invoke();
}
