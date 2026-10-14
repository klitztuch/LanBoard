using LanBoard.Core.Entities;

namespace LanBoard.Application.Admin;

public interface IAdminService
{
    Task<IReadOnlyList<LanParty>> GetAllPartiesAsync(CancellationToken ct = default);
    Task<LanParty> CreatePartyAsync(string name, DateTime date, string location, Guid createdByUserId, CancellationToken ct = default);
    Task UpdatePartyAsync(Guid id, string name, DateTime date, string location, Guid performedByUserId, CancellationToken ct = default);
    Task DeletePartyAsync(Guid id, Guid performedByUserId, CancellationToken ct = default);
    Task<LanParty?> GetPartyWithSeatsAsync(Guid id, CancellationToken ct = default);
    Task SetActivePartyAsync(Guid partyId, Guid performedByUserId, CancellationToken ct = default);
    Task<Seat> AddSeatAsync(Guid partyId, int x, int y, Guid performedByUserId, CancellationToken ct = default);
    Task RemoveSeatAsync(Guid seatId, Guid performedByUserId, CancellationToken ct = default);
    Task<IReadOnlyList<AdminAuditLogEntry>> GetRecentAuditLogAsync(Guid performedByUserId, int count = 50, CancellationToken ct = default);
}
