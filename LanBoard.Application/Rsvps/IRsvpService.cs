using LanBoard.Core.Entities;

namespace LanBoard.Application.Rsvps;

public interface IRsvpService
{
    Task<Rsvp?> GetMyRsvpAsync(Guid userId, Guid partyId, CancellationToken ct = default);
    Task<Rsvp> SetRsvpAsync(Guid userId, Guid partyId, bool isAttending, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, Rsvp>> GetRsvpsByPartyAsync(Guid partyId, CancellationToken ct = default);
}
