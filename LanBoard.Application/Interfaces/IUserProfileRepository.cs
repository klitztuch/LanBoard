using LanBoard.Application.Notifications;
using LanBoard.Core.Entities;

namespace LanBoard.Application.Interfaces;

public interface IUserProfileRepository
{
    /// Read-only, including FieldValues; null if the user never saved settings.
    Task<UserProfile?> GetAsync(Guid userId, CancellationToken ct = default);

    /// Tracked, including FieldValues and User, for the settings save path.
    Task<UserProfile?> GetForUpdateAsync(Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<UserProfile>> GetByUserIdsAsync(IEnumerable<Guid> userIds, CancellationToken ct = default);

    /// Users who opted into the category and have a contact e-mail.
    Task<IReadOnlyList<NotificationRecipient>> GetEmailRecipientsAsync(string category, CancellationToken ct = default);

    Task AddAsync(UserProfile profile, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
