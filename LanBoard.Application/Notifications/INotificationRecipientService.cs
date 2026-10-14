namespace LanBoard.Application.Notifications;

public interface INotificationRecipientService
{
    /// <summary>Users who opted into <paramref name="category"/> and have a contact e-mail. Unknown or non-e-mail category → BusinessRuleException.</summary>
    Task<IReadOnlyList<NotificationRecipient>> GetEmailRecipientsAsync(string category, CancellationToken ct = default);

    /// <summary>The user's in-app preferences; <see cref="InAppNotificationSettings.Default"/> if they never saved settings.</summary>
    Task<InAppNotificationSettings> GetInAppSettingsAsync(Guid userId, CancellationToken ct = default);
}

public sealed record InAppNotificationSettings(IReadOnlySet<string> OptOuts, bool SoundEnabled)
{
    public static InAppNotificationSettings Default { get; } = new(new HashSet<string>(), true);

    public bool IsEnabled(string category) => !OptOuts.Contains(category);
}
