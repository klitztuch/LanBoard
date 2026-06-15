using System.Text.Json.Serialization;

namespace LanBoard.Infrastructure.ApiClients.Steam.Models;

internal sealed record SteamPlayerData([property: JsonPropertyName("avatarfull")] string? AvatarFull);
