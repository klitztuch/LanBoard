namespace LanBoard.Application.Notifications;

public interface ILanBoardNotifier
{
    IReadOnlyCollection<Guid> OnlineUserIds { get; }
    event Action? OnChange;
    void SetUserOnline(Guid userId);
    void SetUserOffline(Guid userId);
    void NotifyChanged();
}
