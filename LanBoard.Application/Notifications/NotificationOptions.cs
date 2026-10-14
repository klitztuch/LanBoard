namespace LanBoard.Application.Notifications;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>Minimum offline time before coming back online counts as a new attendee.online event.</summary>
    public int OnlineGracePeriodSeconds { get; set; } = 60;
}
