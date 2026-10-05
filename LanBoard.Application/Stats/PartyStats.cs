namespace LanBoard.Application.Stats;

public record GameStat(string GameName, TimeSpan TotalPlaytime, int PlayerCount);

public record UserStat(Guid UserId, string DisplayName, string? AvatarUrl, TimeSpan TotalPlaytime);

public record PartyStats(IReadOnlyList<GameStat> TopGames, IReadOnlyList<UserStat> TopUsers, TimeSpan TotalPlaytime);
