using LanBoard.Application.Interfaces;
using LanBoard.Application.Users;
using LanBoard.Core.Entities;

namespace LanBoard.Infrastructure.Services;

public class UserService(IUserRepository users) : IUserService
{
    public Task<IReadOnlyList<User>> GetAllWithIdentitiesAsync(CancellationToken ct = default)
        => users.GetAllWithIdentitiesAsync(ct);

    public async Task<User> GetOrCreateAndSyncSteamProfileAsync(string steamId, string displayName, string? avatarUrl, CancellationToken ct = default)
    {
        var user = await users.FindByProviderAsync("Steam", steamId, ct);

        if (user is not null)
        {
            user.DisplayName = displayName;
            user.AvatarUrl = avatarUrl;
            await users.SaveChangesAsync(ct);
            return user;
        }

        user = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName,
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
