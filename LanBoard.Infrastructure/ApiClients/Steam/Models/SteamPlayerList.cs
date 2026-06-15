using System.Text.Json.Serialization;

namespace LanBoard.Infrastructure.ApiClients.Steam.Models;

internal sealed record SteamPlayerList([property: JsonPropertyName("players")] List<SteamPlayerData>? Players);
