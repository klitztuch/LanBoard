using LanBoard.Core.Entities;

namespace LanBoard.Application.Interfaces;

public interface ISeatRepository : IRepository<Seat>
{
    Task<IReadOnlyList<Seat>> GetByPartyAsync(Guid partyId, CancellationToken ct = default);
    Task<Seat?> GetByUserAsync(Guid partyId, Guid userId, CancellationToken ct = default);
}
