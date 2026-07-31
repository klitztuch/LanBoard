using System.Net.Http.Json;
using LanBoard.Infrastructure.ApiClients.Steam.Models;
using Microsoft.Extensions.Options;

namespace LanBoard.Infrastructure.ApiClients.Steam;

public class SteamApiClient(HttpClient httpClient, IOptions<SteamConfiguration> options) : ISteamApiClient
{
    private const int MaxSteamIdsPerRequest = 100;

    public async Task<string?> GetAvatarUrlAsync(string steamId, CancellationToken ct = default)
    {
        var players = await GetPlayerSummariesAsync([steamId], ct);
        return players.FirstOrDefault()?.AvatarFull;
    }

    public async Task<IReadOnlyList<SteamPlayerSummary>> GetPlayerSummariesAsync(IEnumerable<string> steamIds, CancellationToken ct = default)
    {
        var idList = steamIds.ToList();
        if (idList.Count == 0)
            return [];

        var results = new List<SteamPlayerSummary>(idList.Count);

        foreach (var batch in idList.Chunk(MaxSteamIdsPerRequest))
        {
            var ids = string.Join(',', batch);
            var response = await httpClient.GetFromJsonAsync<SteamApiResponse>(
                $"ISteamUser/GetPlayerSummaries/v2/?key={options.Value.ApiKey}&steamids={ids}", ct);

            if (response?.Response?.Players is not null)
            {
                results.AddRange(response.Response.Players
                    .Select(p => new SteamPlayerSummary(p.SteamId, p.AvatarFull, p.GameId, p.GameExtraInfo)));
            }
        }

        return results;
    }
}
