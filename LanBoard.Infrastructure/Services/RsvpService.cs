using LanBoard.Application.Interfaces;
using LanBoard.Application.Rsvps;
using LanBoard.Core.Entities;

namespace LanBoard.Infrastructure.Services;

public class RsvpService(IRsvpRepository rsvps) : IRsvpService
{
    public Task<Rsvp?> GetMyRsvpAsync(Guid userId, Guid partyId, CancellationToken ct = default)
        => rsvps.GetByUserAndPartyAsync(userId, partyId, ct);

    public async Task<Rsvp> SetRsvpAsync(Guid userId, Guid partyId, bool isAttending, CancellationToken ct = default)
    {
        var existing = await rsvps.GetByUserAndPartyAsync(userId, partyId, ct);
        if (existing is not null)
        {
            existing.IsAttending = isAttending;
            existing.RespondedAt = DateTime.UtcNow;
            await rsvps.SaveChangesAsync(ct);
            return existing;
        }

        var rsvp = new Rsvp
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PartyId = partyId,
            IsAttending = isAttending,
            RespondedAt = DateTime.UtcNow
        };
        await rsvps.AddAsync(rsvp, ct);
        await rsvps.SaveChangesAsync(ct);
        return rsvp;
    }

    public async Task<IReadOnlyDictionary<Guid, Rsvp>> GetRsvpsByPartyAsync(Guid partyId, CancellationToken ct = default)
        => (await rsvps.GetByPartyAsync(partyId, ct)).ToDictionary(r => r.UserId);
}
