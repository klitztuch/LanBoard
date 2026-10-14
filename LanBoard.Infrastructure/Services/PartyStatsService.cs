using LanBoard.Application.Interfaces;
using LanBoard.Application.Stats;

namespace LanBoard.Infrastructure.Services;

public class PartyStatsService(ISessionRepository sessions) : IPartyStatsService
{
    public async Task<PartyStats> GetStatsAsync(Guid partyId, CancellationToken ct = default)
    {
        var all = await sessions.GetByPartyAsync(partyId, ct);

        var topGames = all
            .Where(s => s.GameName is not null)
            .GroupBy(s => s.GameName!)
            .Select(g => new GameStat(g.Key, SessionPlaytime.Sum(g), g.Select(s => s.UserId).Distinct().Count()))
            .OrderByDescending(g => g.TotalPlaytime)
            .Take(10)
            .ToList();

        var topUsers = all
            .GroupBy(s => s.UserId)
            .Select(g => new UserStat(g.Key, g.First().User.DisplayName, g.First().User.AvatarUrl, SessionPlaytime.Sum(g)))
            .OrderByDescending(u => u.TotalPlaytime)
            .Take(10)
            .ToList();

        var totalPlaytime = SessionPlaytime.Sum(all);

        return new PartyStats(topGames, topUsers, totalPlaytime);
    }
}
