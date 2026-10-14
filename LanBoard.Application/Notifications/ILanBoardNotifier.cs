namespace LanBoard.Application.Notifications;

public interface ILanBoardNotifier
{
    IReadOnlyCollection<Guid> OnlineUserIds { get; }
    event Action? OnChange;

    /// <summary>
    /// Registers a connection. True iff this is the user's first connection AND they were offline
    /// for at least the grace period (or have never been seen since app start).
    /// </summary>
    bool SetUserOnline(Guid userId);
    void SetUserOffline(Guid userId);
    void NotifyChanged();
}
