using LanBoard.Application.Interfaces;
using LanBoard.Application.Notifications;
using LanBoard.Application.Seats;
using LanBoard.Core.Entities;

namespace LanBoard.Infrastructure.Services;

public class SeatService(ILanPartyRepository parties, ISeatRepository seats, ILanBoardNotifier notifier) : ISeatService
{
    public async Task<LanParty?> GetActivePartyWithSeatsAsync(CancellationToken ct = default)
    {
        var party = await parties.GetActiveAsync(ct);
        if (party is null) return null;
        return await parties.GetWithSeatsAndSessionsAsync(party.Id, ct);
    }

    public Task<IReadOnlyList<Seat>> GetSeatsByPartyAsync(Guid partyId, CancellationToken ct = default)
        => seats.GetByPartyAsync(partyId, ct);

    public async Task<Seat> ClaimSeatAsync(Guid seatId, Guid userId, CancellationToken ct = default)
    {
        var seat = await seats.GetByIdAsync(seatId, ct)
            ?? throw new InvalidOperationException("Seat not found.");

        if (seat.AssignedUserId is not null && seat.AssignedUserId != userId)
            throw new InvalidOperationException("Seat is already taken.");

        var current = await seats.GetByUserAsync(seat.PartyId, userId, ct);
        if (current is not null && current.Id != seatId)
            current.AssignedUserId = null;

        seat.AssignedUserId = userId;
        await seats.SaveChangesAsync(ct);
        notifier.NotifyChanged();
        return seat;
    }

    public async Task ReleaseSeatAsync(Guid seatId, Guid userId, CancellationToken ct = default)
    {
        var seat = await seats.GetByIdAsync(seatId, ct)
            ?? throw new InvalidOperationException("Seat not found.");

        if (seat.AssignedUserId != userId)
            throw new InvalidOperationException("Seat is not assigned to this user.");

        seat.AssignedUserId = null;
        await seats.SaveChangesAsync(ct);
        notifier.NotifyChanged();
    }
}
