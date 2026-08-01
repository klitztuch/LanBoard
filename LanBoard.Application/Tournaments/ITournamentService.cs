using LanBoard.Core.Entities;

namespace LanBoard.Application.Tournaments;

public interface ITournamentService
{
    Task<IReadOnlyList<Tournament>> GetByPartyAsync(Guid partyId, CancellationToken ct = default);
    Task<Tournament> CreateAsync(Guid partyId, string name, CancellationToken ct = default);
    Task<Tournament?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task AddParticipantAsync(Guid tournamentId, Guid userId, CancellationToken ct = default);
    Task RemoveParticipantAsync(Guid tournamentId, Guid userId, CancellationToken ct = default);
    Task StartAsync(Guid tournamentId, CancellationToken ct = default);
    Task SetMatchWinnerAsync(Guid tournamentId, Guid matchId, Guid winnerParticipantId, CancellationToken ct = default);
}
