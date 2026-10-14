using LanBoard.Core.Entities;

namespace LanBoard.Application.Interfaces;

public interface IRsvpRepository : IRepository<Rsvp>
{
    Task<Rsvp?> GetByUserAndPartyAsync(Guid userId, Guid partyId, CancellationToken ct = default);
    Task<IReadOnlyList<Rsvp>> GetByPartyAsync(Guid partyId, CancellationToken ct = default);
    Task<IReadOnlyList<LanParty>> GetAttendedPastPartiesAsync(Guid userId, DateTime before, CancellationToken ct = default); // newest first
    Task<IReadOnlyList<User>> GetAttendingUsersAsync(Guid partyId, CancellationToken ct = default); // read-only, by DisplayName
}
