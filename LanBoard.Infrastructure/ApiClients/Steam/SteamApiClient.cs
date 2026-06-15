using System.Net.Http.Json;
using LanBoard.Infrastructure.ApiClients.Steam.Models;
using Microsoft.Extensions.Options;

namespace LanBoard.Infrastructure.ApiClients.Steam;

public class SteamApiClient(HttpClient httpClient, IOptions<SteamConfiguration> options) : ISteamApiClient
{
    public async Task<string?> GetAvatarUrlAsync(string steamId)
    {
        var response = await httpClient.GetFromJsonAsync<SteamApiResponse>(
            $"ISteamUser/GetPlayerSummaries/v2/?key={options.Value.ApiKey}&steamids={steamId}");
        return response?.Response?.Players?.FirstOrDefault()?.AvatarFull;
    }
}
