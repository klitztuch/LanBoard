namespace LanBoard.Application.Notifications;

[Flags]
public enum NotificationChannels
{
    None = 0,
    Email = 1,
    InApp = 2,
}

// Notification categories and the channels each one supports; labels are localized in the Web layer
// (NotificationCategory.<key>), in-app toast texts as Notification.<key>.
public static class NotificationCategories
{
    public const string PartyAnnouncements = "party.announcements";
    public const string TournamentUpdates = "tournament.updates";
    public const string AttendeeOnline = "attendee.online";

    public static IReadOnlyList<string> All { get; } = ConstantCatalog.Of(typeof(NotificationCategories));

    private static readonly Dictionary<string, NotificationChannels> ChannelMap = new()
    {
        [PartyAnnouncements] = NotificationChannels.Email,
        [TournamentUpdates] = NotificationChannels.Email,
        [AttendeeOnline] = NotificationChannels.InApp,
    };

    public static NotificationChannels ChannelsOf(string category)
        => ChannelMap.GetValueOrDefault(category, NotificationChannels.None);

    /// <summary>Categories offering <paramref name="channel"/>, in catalog order.</summary>
    public static IReadOnlyList<string> Supporting(NotificationChannels channel)
        => All.Where(c => ChannelsOf(c).HasFlag(channel)).ToList();
}
