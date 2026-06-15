using LanBoard.Core.Entities;

namespace LanBoard.Application.Users;

public interface IUserService
{
    Task<User> GetOrCreateAndSyncSteamProfileAsync(string steamId, string displayName, string? avatarUrl, CancellationToken ct = default);
}
