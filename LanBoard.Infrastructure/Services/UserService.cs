using LanBoard.Application.Errors;
using LanBoard.Application.Interfaces;
using LanBoard.Application.Users;
using LanBoard.Core.Entities;

namespace LanBoard.Infrastructure.Services;

public class UserService(IUserRepository users) : IUserService
{
    public Task<IReadOnlyList<User>> GetAllWithIdentitiesAsync(CancellationToken ct = default)
        => users.GetAllWithIdentitiesAsync(ct);

    public Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default)
        => users.GetWithIdentitiesAsync(userId, ct);

    public async Task SetPreferredCultureAsync(Guid userId, string culture, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(userId, ct)
            ?? throw new BusinessRuleException(ErrorCodes.UserNotFound);
        user.PreferredCulture = culture;
        await users.SaveChangesAsync(ct);
    }

    public async Task<User> GetOrCreateAndSyncSteamProfileAsync(string steamId, string displayName, string? avatarUrl, CancellationToken ct = default)
    {
        var user = await users.FindByProviderAsync("Steam", steamId, ct);

        if (user is not null)
        {
            user.SyncSteamName(displayName);
            user.AvatarUrl = avatarUrl;
            await users.SaveChangesAsync(ct);
            return user;
        }

        user = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName,
            SteamName = displayName,
            AvatarUrl = avatarUrl,
            CreatedAt = DateTime.UtcNow,
            Identities =
            [
                new UserIdentity
                {
                    Id = Guid.NewGuid(),
                    Provider = "Steam",
                    ProviderUserId = steamId,
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };

        await users.AddAsync(user, ct);
        await users.SaveChangesAsync(ct);
        return user;
    }
}
