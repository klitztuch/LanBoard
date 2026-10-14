using LanBoard.Core.Entities;

namespace LanBoard.Application.Interfaces;

public interface ITournamentParticipantRepository : IRepository<TournamentParticipant>
{
    Task<IReadOnlyList<TournamentParticipant>> GetByTournamentAsync(Guid tournamentId, CancellationToken ct = default);
    Task<TournamentParticipant?> FindAsync(Guid tournamentId, Guid userId, CancellationToken ct = default);
}
