using LanBoard.Core.Entities;

namespace LanBoard.Application.Interfaces;

public interface ITournamentMatchRepository : IRepository<TournamentMatch>
{
    Task<TournamentMatch?> FindAsync(Guid tournamentId, int round, int slot, CancellationToken ct = default);
}
