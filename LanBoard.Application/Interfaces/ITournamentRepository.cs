using LanBoard.Core.Entities;

namespace LanBoard.Application.Interfaces;

public interface ITournamentRepository : IRepository<Tournament>
{
    Task<IReadOnlyList<Tournament>> GetByPartyAsync(Guid partyId, CancellationToken ct = default);
    Task<Tournament?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);
}
