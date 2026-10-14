namespace LanBoard.Application.Notifications;

public sealed record NotificationRecipient(Guid UserId, string DisplayName, string Email, string? PreferredCulture);
