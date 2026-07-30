using LanBoard.Infrastructure.ApiClients.Steam.Models;

namespace LanBoard.Infrastructure.ApiClients.Steam;

public interface ISteamApiClient
{
    Task<string?> GetAvatarUrlAsync(string steamId, CancellationToken ct = default);
    Task<IReadOnlyList<SteamPlayerSummary>> GetPlayerSummariesAsync(IEnumerable<string> steamIds, CancellationToken ct = default);
}
