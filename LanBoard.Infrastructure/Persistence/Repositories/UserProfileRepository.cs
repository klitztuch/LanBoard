using LanBoard.Application.Interfaces;
using LanBoard.Application.Notifications;
using LanBoard.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace LanBoard.Infrastructure.Persistence.Repositories;

public class UserProfileRepository(AppDbContext db) : IUserProfileRepository
{
    public async Task<UserProfile?> GetAsync(Guid userId, CancellationToken ct = default)
        => await db.UserProfiles
            .AsNoTracking()
            .Include(p => p.FieldValues)
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

    public async Task<UserProfile?> GetForUpdateAsync(Guid userId, CancellationToken ct = default)
        => await db.UserProfiles
            .Include(p => p.FieldValues)
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

    public async Task<IReadOnlyList<UserProfile>> GetByUserIdsAsync(IEnumerable<Guid> userIds, CancellationToken ct = default)
    {
        var ids = userIds.ToList();
        return await db.UserProfiles
            .AsNoTracking()
            .Include(p => p.FieldValues)
            .Where(p => ids.Contains(p.UserId))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<NotificationRecipient>> GetEmailRecipientsAsync(string category, CancellationToken ct = default)
        => await db.UserProfiles
            .AsNoTracking()
            .Where(p => p.ContactEmail != null && p.NotificationOptIns.Contains(category))
            .OrderBy(p => p.User.DisplayName)
            .Select(p => new NotificationRecipient(p.UserId, p.User.DisplayName, p.ContactEmail!, p.User.PreferredCulture))
            .ToListAsync(ct);

    public async Task<InAppNotificationSettings?> GetInAppSettingsAsync(Guid userId, CancellationToken ct = default)
    {
        var row = await db.UserProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new { p.InAppOptOuts, p.NotificationSoundEnabled })
            .FirstOrDefaultAsync(ct);
        return row is null ? null : new InAppNotificationSettings(row.InAppOptOuts.ToHashSet(), row.NotificationSoundEnabled);
    }

    public async Task AddAsync(UserProfile profile, CancellationToken ct = default)
        => await db.UserProfiles.AddAsync(profile, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
