using LanBoard.Application.Notifications;

namespace LanBoard.Web.Services;

/// <summary>
/// Broadcasts notifications to every connected circuit; each <c>NotificationHost</c> filters for its own user.
/// </summary>
public sealed class InAppNotificationHub(ILogger<InAppNotificationHub> logger) : INotificationPublisher
{
    public event Action<NotificationMessage>? Published;

    /// <summary>Raised with the user id after that user saved notification settings.</summary>
    public event Action<Guid>? PreferencesChanged;

    public void Publish(NotificationMessage message) => Raise(Published, message);

    public void NotifyPreferencesChanged(Guid userId) => Raise(PreferencesChanged, userId);

    // Each subscriber on its own, so one broken circuit can't stop delivery to the others.
    private void Raise<T>(Action<T>? handlers, T arg)
    {
        if (handlers is null) return;
        foreach (var handler in handlers.GetInvocationList().Cast<Action<T>>())
        {
            try
            {
                handler(arg);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Notification subscriber failed for {Argument}", arg);
            }
        }
    }
}
