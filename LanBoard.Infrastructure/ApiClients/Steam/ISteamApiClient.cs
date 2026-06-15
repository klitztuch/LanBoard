namespace LanBoard.Infrastructure.ApiClients.Steam;

public interface ISteamApiClient
{
    Task<string?> GetAvatarUrlAsync(string steamId);
}
