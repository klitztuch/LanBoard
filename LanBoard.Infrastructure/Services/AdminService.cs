using LanBoard.Application.Admin;
using LanBoard.Application.Interfaces;
using LanBoard.Core.Entities;

namespace LanBoard.Infrastructure.Services;

public class AdminService(ILanPartyRepository parties, ISeatRepository seats) : IAdminService
{
    public Task<IReadOnlyList<LanParty>> GetAllPartiesAsync(CancellationToken ct = default)
        => parties.GetAllAsync(ct);

    public async Task<LanParty> CreatePartyAsync(string name, DateTime date, string location, Guid createdByUserId, CancellationToken ct = default)
    {
        var party = new LanParty
        {
            Id = Guid.NewGuid(),
            Name = name,
            Date = date,
            Location = location,
            InviteCode = GenerateInviteCode(),
            CreatedByUserId = createdByUserId
        };
        await parties.AddAsync(party, ct);
        await parties.SaveChangesAsync(ct);
        return party;
    }

    public async Task UpdatePartyAsync(Guid id, string name, DateTime date, string location, CancellationToken ct = default)
    {
        var party = await parties.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("Party not found.");
        party.Name = name;
        party.Date = date;
        party.Location = location;
        await parties.SaveChangesAsync(ct);
    }

    public async Task DeletePartyAsync(Guid id, CancellationToken ct = default)
    {
        var party = await parties.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("Party not found.");
        parties.Remove(party);
        await parties.SaveChangesAsync(ct);
    }

    public Task<LanParty?> GetPartyWithSeatsAsync(Guid id, CancellationToken ct = default)
        => parties.GetWithSeatsAndSessionsAsync(id, ct);

    public async Task<Seat> AddSeatAsync(Guid partyId, int x, int y, CancellationToken ct = default)
    {
        var seat = new Seat
        {
            Id = Guid.NewGuid(),
            PartyId = partyId,
            X = x,
            Y = y,
            Label = $"{x}{y}"
        };
        await seats.AddAsync(seat, ct);
        await seats.SaveChangesAsync(ct);
        return seat;
    }

    public async Task RemoveSeatAsync(Guid seatId, CancellationToken ct = default)
    {
        var seat = await seats.GetByIdAsync(seatId, ct)
            ?? throw new InvalidOperationException("Seat not found.");
        if (seat.AssignedUserId is not null)
            throw new InvalidOperationException("Seat is currently assigned to a user.");
        seats.Remove(seat);
        await seats.SaveChangesAsync(ct);
    }

    private static string GenerateInviteCode()
        => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
}
