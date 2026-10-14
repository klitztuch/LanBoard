using LanBoard.Application.Errors;
using LanBoard.Application.Interfaces;
using LanBoard.Application.Notifications;
using LanBoard.Application.Seats;
using LanBoard.Core.Entities;

namespace LanBoard.Infrastructure.Services;

public class SeatService(ILanPartyRepository parties, ISeatRepository seats, ILanBoardNotifier notifier) : ISeatService
{
    public async Task<LanParty?> GetActivePartyWithSeatsAsync(Guid? partyId = null, CancellationToken ct = default)
    {
        if (partyId is { } id)
            return await parties.GetWithSeatsAndSessionsAsync(id, ct);

        var party = await parties.GetActiveAsync(ct);
        if (party is null) return null;
        return await parties.GetWithSeatsAndSessionsAsync(party.Id, ct);
    }

    public Task<IReadOnlyList<Seat>> GetSeatsByPartyAsync(Guid partyId, CancellationToken ct = default)
        => seats.GetByPartyAsync(partyId, ct);

    public async Task<Seat> ClaimSeatAsync(Guid seatId, Guid userId, CancellationToken ct = default)
    {
        var seat = await seats.GetByIdAsync(seatId, ct)
            ?? throw new BusinessRuleException(ErrorCodes.SeatNotFound);

        if (seat.AssignedUserId is not null && seat.AssignedUserId != userId)
            throw new BusinessRuleException(ErrorCodes.SeatTaken);

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
            ?? throw new BusinessRuleException(ErrorCodes.SeatNotFound);

        if (seat.AssignedUserId != userId)
            throw new BusinessRuleException(ErrorCodes.SeatNotAssignedToUser);

        seat.AssignedUserId = null;
        await seats.SaveChangesAsync(ct);
        notifier.NotifyChanged();
    }
}
