using System.Text.Json.Serialization;

namespace LanBoard.Infrastructure.ApiClients.Steam.Models;

internal sealed record SteamPlayerData(
    [property: JsonPropertyName("steamid")] string SteamId,
    [property: JsonPropertyName("avatarfull")] string? AvatarFull,
    [property: JsonPropertyName("gameid")] string? GameId,
    [property: JsonPropertyName("gameextrainfo")] string? GameExtraInfo);
