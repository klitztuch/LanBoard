using LanBoard.Core.Entities;

namespace LanBoard.Application.Seats;

public interface ISeatService
{
    Task<LanParty?> GetActivePartyWithSeatsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Seat>> GetSeatsByPartyAsync(Guid partyId, CancellationToken ct = default);
    Task<Seat> ClaimSeatAsync(Guid seatId, Guid userId, CancellationToken ct = default);
    Task ReleaseSeatAsync(Guid seatId, Guid userId, CancellationToken ct = default);
}
