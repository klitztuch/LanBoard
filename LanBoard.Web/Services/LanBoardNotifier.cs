using System.Collections.Concurrent;
using LanBoard.Application.Notifications;

namespace LanBoard.Web.Services;

public class LanBoardNotifier : ILanBoardNotifier
{
    private readonly ConcurrentDictionary<Guid, int> _online = new();

    public IReadOnlyCollection<Guid> OnlineUserIds => [.. _online.Keys];

    public event Action? OnChange;

    public void SetUserOnline(Guid userId)
    {
        _online.AddOrUpdate(userId, 1, (_, currentCount) => currentCount + 1);
        OnChange?.Invoke();
    }

    public void SetUserOffline(Guid userId)
    {
        _online.AddOrUpdate(userId, 0, (_, currentCount) => currentCount - 1);
        if (_online.TryGetValue(userId, out var count) && count <= 0)
            _online.TryRemove(userId, out _);
        OnChange?.Invoke();
    }

    public void NotifyChanged() => OnChange?.Invoke();
}
