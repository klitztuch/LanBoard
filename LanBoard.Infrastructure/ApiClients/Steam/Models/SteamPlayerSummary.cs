namespace LanBoard.Infrastructure.ApiClients.Steam.Models;

public sealed record SteamPlayerSummary(string SteamId, string? AvatarFull, string? GameId, string? GameExtraInfo);
