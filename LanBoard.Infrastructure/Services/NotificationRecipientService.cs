using LanBoard.Application.Errors;
using LanBoard.Application.Interfaces;
using LanBoard.Application.Notifications;

namespace LanBoard.Infrastructure.Services;

// The only way contact e-mails leave the Application layer apart from the owner's own settings.
public class NotificationRecipientService(IUserProfileRepository profiles) : INotificationRecipientService
{
    public Task<IReadOnlyList<NotificationRecipient>> GetEmailRecipientsAsync(string category, CancellationToken ct = default)
    {
        if (!NotificationCategories.Supporting(NotificationChannels.Email).Contains(category))
            throw new BusinessRuleException(ErrorCodes.NotificationUnknownCategory, category);

        return profiles.GetEmailRecipientsAsync(category, ct);
    }

    public async Task<InAppNotificationSettings> GetInAppSettingsAsync(Guid userId, CancellationToken ct = default)
        => await profiles.GetInAppSettingsAsync(userId, ct) ?? InAppNotificationSettings.Default;
}
