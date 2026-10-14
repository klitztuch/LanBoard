namespace LanBoard.Application.Notifications;

/// <summary>Fire-and-forget delivery to all connected recipients; never throws for delivery failures.</summary>
public interface INotificationPublisher
{
    void Publish(NotificationMessage message);
}

/// <param name="Category">Key from <see cref="NotificationCategories"/>.</param>
/// <param name="ActorUserId">User who caused the event; never notified.</param>
/// <param name="Args">Positional format args for the resource <c>Notification.{Category}</c>.</param>
public sealed record NotificationMessage(string Category, Guid? ActorUserId, IReadOnlyList<string> Args);
