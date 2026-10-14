namespace LanBoard.Application.Notifications;

public interface INotificationRecipientService
{
    /// <summary>Users who opted into <paramref name="category"/> and have a contact e-mail. Unknown category → BusinessRuleException.</summary>
    Task<IReadOnlyList<NotificationRecipient>> GetEmailRecipientsAsync(string category, CancellationToken ct = default);
}
