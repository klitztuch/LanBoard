using LanBoard.Core.Entities;

namespace LanBoard.Application.Memberships;

public interface IPartyMembershipService
{
    Task<bool> IsMemberAsync(Guid userId, Guid partyId, CancellationToken ct = default);
    Task<LanParty> JoinByInviteCodeAsync(Guid userId, string inviteCode, CancellationToken ct = default);
}
