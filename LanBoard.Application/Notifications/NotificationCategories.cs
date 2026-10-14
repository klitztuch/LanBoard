namespace LanBoard.Application.Notifications;

// Opt-in categories for e-mail notifications; labels are localized in the Web layer (NotificationCategory.<key>).
public static class NotificationCategories
{
    public const string PartyAnnouncements = "party.announcements";
    public const string TournamentUpdates = "tournament.updates";

    public static IReadOnlyList<string> All { get; } = ConstantCatalog.Of(typeof(NotificationCategories));
}
