using LanBoard.Core.Entities;

namespace LanBoard.Application.Interfaces;

public interface IPartyMembershipRepository : IRepository<PartyMembership>
{
    Task<bool> ExistsAsync(Guid userId, Guid partyId, CancellationToken ct = default);
}
