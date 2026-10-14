using LanBoard.Application.Notifications;

namespace LanBoard.Web.Notifications;

public static class InAppNotificationFilter
{
    public static bool ShouldShow(NotificationMessage message, Guid recipientId, InAppNotificationSettings settings)
        => message.ActorUserId != recipientId
           && NotificationCategories.ChannelsOf(message.Category).HasFlag(NotificationChannels.InApp)
           && settings.IsEnabled(message.Category);
}
