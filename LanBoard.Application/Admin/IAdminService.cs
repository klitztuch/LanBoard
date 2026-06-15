using LanBoard.Core.Entities;

namespace LanBoard.Application.Admin;

public interface IAdminService
{
    Task<IReadOnlyList<LanParty>> GetAllPartiesAsync(CancellationToken ct = default);
    Task<LanParty> CreatePartyAsync(string name, DateTime date, string location, Guid createdByUserId, CancellationToken ct = default);
    Task UpdatePartyAsync(Guid id, string name, DateTime date, string location, CancellationToken ct = default);
    Task DeletePartyAsync(Guid id, CancellationToken ct = default);
    Task<LanParty?> GetPartyWithSeatsAsync(Guid id, CancellationToken ct = default);
    Task<Seat> AddSeatAsync(Guid partyId, int x, int y, CancellationToken ct = default);
    Task RemoveSeatAsync(Guid seatId, CancellationToken ct = default);
}
