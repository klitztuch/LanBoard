using LanBoard.Application.Interfaces;
using LanBoard.Application.Memberships;
using LanBoard.Core.Entities;

namespace LanBoard.Infrastructure.Services;

public class PartyMembershipService(ILanPartyRepository parties, IPartyMembershipRepository memberships) : IPartyMembershipService
{
    public Task<bool> IsMemberAsync(Guid userId, Guid partyId, CancellationToken ct = default)
        => memberships.ExistsAsync(userId, partyId, ct);

    public async Task<LanParty> JoinByInviteCodeAsync(Guid userId, string inviteCode, CancellationToken ct = default)
    {
        var party = await parties.FindByInviteCodeAsync(inviteCode.Trim(), ct)
            ?? throw new InvalidOperationException("Ungültiger Invite-Code.");

        if (await memberships.ExistsAsync(userId, party.Id, ct))
            return party;

        await memberships.AddAsync(new PartyMembership
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PartyId = party.Id,
            JoinedAt = DateTime.UtcNow
        }, ct);
        await memberships.SaveChangesAsync(ct);

        return party;
    }
}
