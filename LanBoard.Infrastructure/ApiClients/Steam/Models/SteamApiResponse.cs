using System.Text.Json.Serialization;

namespace LanBoard.Infrastructure.ApiClients.Steam.Models;

internal sealed record SteamApiResponse([property: JsonPropertyName("response")] SteamPlayerList? Response);
