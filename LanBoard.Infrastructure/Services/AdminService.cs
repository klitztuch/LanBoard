using LanBoard.Application.Admin;
using LanBoard.Application.Interfaces;
using LanBoard.Core.Entities;

namespace LanBoard.Infrastructure.Services;

public class AdminService(ILanPartyRepository parties, ISeatRepository seats, IAdminAuditLogRepository auditLog) : IAdminService
{
    public Task<IReadOnlyList<LanParty>> GetAllPartiesAsync(CancellationToken ct = default)
        => parties.GetAllAsync(ct);

    public async Task<LanParty> CreatePartyAsync(string name, DateTime date, string location, Guid createdByUserId, CancellationToken ct = default)
    {
        var party = new LanParty
        {
            Id = Guid.NewGuid(),
            Name = name,
            Date = AsUtc(date),
            Location = location,
            InviteCode = GenerateInviteCode(),
            CreatedByUserId = createdByUserId
        };
        await parties.AddAsync(party, ct);
        await parties.SaveChangesAsync(ct);
        await LogAsync(createdByUserId, "PartyCreated", $"Party \"{name}\" erstellt", ct);
        return party;
    }

    public async Task UpdatePartyAsync(Guid id, string name, DateTime date, string location, Guid performedByUserId, CancellationToken ct = default)
    {
        var party = await parties.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("Party not found.");
        party.Name = name;
        party.Date = AsUtc(date);
        party.Location = location;
        await parties.SaveChangesAsync(ct);
        await LogAsync(performedByUserId, "PartyUpdated", $"Party \"{name}\" bearbeitet", ct);
    }

    public async Task DeletePartyAsync(Guid id, Guid performedByUserId, CancellationToken ct = default)
    {
        var party = await parties.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("Party not found.");
        parties.Remove(party);
        await parties.SaveChangesAsync(ct);
        await LogAsync(performedByUserId, "PartyDeleted", $"Party \"{party.Name}\" gelöscht", ct);
    }

    public Task<LanParty?> GetPartyWithSeatsAsync(Guid id, CancellationToken ct = default)
        => parties.GetWithSeatsAndSessionsAsync(id, ct);

    public async Task SetActivePartyAsync(Guid partyId, Guid performedByUserId, CancellationToken ct = default)
    {
        // Deactivate and activate in separate SaveChanges calls: the partial unique
        // index on IsActive can't be deferred (Postgres doesn't support deferrable
        // constraints with a WHERE clause), and EF doesn't guarantee statement order
        // between two Modified entities in one SaveChanges batch — writing both
        // changes in one transaction risks two rows being IsActive=true at once,
        // even briefly, and violating the index.
        var currentlyActive = await parties.GetActiveAsync(ct);
        if (currentlyActive is not null && currentlyActive.Id != partyId)
        {
            currentlyActive.IsActive = false;
            await parties.SaveChangesAsync(ct);
        }

        var target = await parties.GetByIdAsync(partyId, ct)
            ?? throw new InvalidOperationException("Party not found.");
        target.IsActive = true;
        await parties.SaveChangesAsync(ct);
        await LogAsync(performedByUserId, "PartyActivated", $"Party \"{target.Name}\" aktiviert", ct);
    }

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

    public Task<IReadOnlyList<AdminAuditLogEntry>> GetRecentAuditLogAsync(int count = 50, CancellationToken ct = default)
        => auditLog.GetRecentAsync(count, ct);

    private async Task LogAsync(Guid userId, string action, string details, CancellationToken ct)
    {
        await auditLog.AddAsync(new AdminAuditLogEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            Details = details,
            CreatedAt = DateTime.UtcNow
        }, ct);
        await auditLog.SaveChangesAsync(ct);
    }

    private static string GenerateInviteCode()
        => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static DateTime AsUtc(DateTime date)
        => DateTime.SpecifyKind(date, DateTimeKind.Utc);
}
