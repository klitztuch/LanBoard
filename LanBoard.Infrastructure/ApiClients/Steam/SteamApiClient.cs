using System.Net.Http.Json;
using LanBoard.Infrastructure.ApiClients.Steam.Models;
using Microsoft.Extensions.Options;

namespace LanBoard.Infrastructure.ApiClients.Steam;

public class SteamApiClient(HttpClient httpClient, IOptions<SteamConfiguration> options) : ISteamApiClient
{
    public async Task<string?> GetAvatarUrlAsync(string steamId, CancellationToken ct = default)
    {
        var players = await GetPlayerSummariesAsync([steamId], ct);
        return players.FirstOrDefault()?.AvatarFull;
    }

    public async Task<IReadOnlyList<SteamPlayerSummary>> GetPlayerSummariesAsync(IEnumerable<string> steamIds, CancellationToken ct = default)
    {
        var ids = string.Join(',', steamIds);
        if (ids.Length == 0)
            return [];

        var response = await httpClient.GetFromJsonAsync<SteamApiResponse>(
            $"ISteamUser/GetPlayerSummaries/v2/?key={options.Value.ApiKey}&steamids={ids}", ct);

        return response?.Response?.Players?
            .Select(p => new SteamPlayerSummary(p.SteamId, p.AvatarFull, p.GameId, p.GameExtraInfo))
            .ToList() ?? [];
    }
}
