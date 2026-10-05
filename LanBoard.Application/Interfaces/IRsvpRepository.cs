using LanBoard.Core.Entities;

namespace LanBoard.Application.Interfaces;

public interface IRsvpRepository : IRepository<Rsvp>
{
    Task<Rsvp?> GetByUserAndPartyAsync(Guid userId, Guid partyId, CancellationToken ct = default);
    Task<IReadOnlyList<Rsvp>> GetByPartyAsync(Guid partyId, CancellationToken ct = default);
}
