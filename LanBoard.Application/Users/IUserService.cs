using LanBoard.Core.Entities;

namespace LanBoard.Application.Users;

public interface IUserService
{
    Task<User> GetOrCreateAndSyncSteamProfileAsync(string steamId, string displayName, string? avatarUrl, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetAllWithIdentitiesAsync(CancellationToken ct = default);

    /// <summary>Read-only snapshot of a user, or null if unknown.</summary>
    Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Stores the user's own language choice. The caller validates <paramref name="culture"/> against the supported cultures.</summary>
    Task SetPreferredCultureAsync(Guid userId, string culture, CancellationToken ct = default);
}
