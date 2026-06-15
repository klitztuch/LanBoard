namespace LanBoard.Infrastructure.ApiClients.Steam;

public class SteamConfiguration
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.steampowered.com/";
}
