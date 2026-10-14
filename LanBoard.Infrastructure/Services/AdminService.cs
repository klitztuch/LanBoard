using LanBoard.Application.Admin;
using LanBoard.Application.Authorization;
using LanBoard.Application.Errors;
using LanBoard.Application.Interfaces;
using LanBoard.Core.Entities;

namespace LanBoard.Infrastructure.Services;

public class AdminService(
    ILanPartyRepository parties,
    ISeatRepository seats,
    IAdminAuditLogRepository auditLog,
    IPermissionService permissions) : IAdminService
{
    public Task<IReadOnlyList<LanParty>> GetAllPartiesAsync(CancellationToken ct = default)
        => parties.GetAllAsync(ct);

    public async Task<LanParty> CreatePartyAsync(string name, DateTime date, string location, Guid createdByUserId, CancellationToken ct = default)
    {
        await permissions.EnsureAsync(createdByUserId, Permissions.PartyManage, ct);

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
        await LogAsync(createdByUserId, AuditActions.PartyCreated, $"Party \"{name}\" created", ct);
        return party;
    }

    public async Task UpdatePartyAsync(Guid id, string name, DateTime date, string location, Guid performedByUserId, CancellationToken ct = default)
    {
        await permissions.EnsureAsync(performedByUserId, Permissions.PartyManage, ct);

        var party = await parties.GetByIdAsync(id, ct)
            ?? throw new BusinessRuleException(ErrorCodes.PartyNotFound);
        party.Name = name;
        party.Date = AsUtc(date);
        party.Location = location;
        await parties.SaveChangesAsync(ct);
        await LogAsync(performedByUserId, AuditActions.PartyUpdated, $"Party \"{name}\" updated", ct);
    }

    public async Task DeletePartyAsync(Guid id, Guid performedByUserId, CancellationToken ct = default)
    {
        await permissions.EnsureAsync(performedByUserId, Permissions.PartyManage, ct);

        var party = await parties.GetByIdAsync(id, ct)
            ?? throw new BusinessRuleException(ErrorCodes.PartyNotFound);
        parties.Remove(party);
        await parties.SaveChangesAsync(ct);
        await LogAsync(performedByUserId, AuditActions.PartyDeleted, $"Party \"{party.Name}\" deleted", ct);
    }

    public Task<LanParty?> GetPartyWithSeatsAsync(Guid id, CancellationToken ct = default)
        => parties.GetWithSeatsAndSessionsAsync(id, ct);

    public async Task SetActivePartyAsync(Guid partyId, Guid performedByUserId, CancellationToken ct = default)
    {
        await permissions.EnsureAsync(performedByUserId, Permissions.PartyManage, ct);

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
            ?? throw new BusinessRuleException(ErrorCodes.PartyNotFound);
        target.IsActive = true;
        await parties.SaveChangesAsync(ct);
        await LogAsync(performedByUserId, AuditActions.PartyActivated, $"Party \"{target.Name}\" activated", ct);
    }

    public async Task<Seat> AddSeatAsync(Guid partyId, int x, int y, Guid performedByUserId, CancellationToken ct = default)
    {
        await permissions.EnsureAsync(performedByUserId, Permissions.PartyManage, ct);

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

    public async Task RemoveSeatAsync(Guid seatId, Guid performedByUserId, CancellationToken ct = default)
    {
        await permissions.EnsureAsync(performedByUserId, Permissions.PartyManage, ct);

        var seat = await seats.GetByIdAsync(seatId, ct)
            ?? throw new BusinessRuleException(ErrorCodes.SeatNotFound);
        if (seat.AssignedUserId is not null)
            throw new BusinessRuleException(ErrorCodes.SeatAssigned);
        seats.Remove(seat);
        await seats.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AdminAuditLogEntry>> GetRecentAuditLogAsync(Guid performedByUserId, int count = 50, CancellationToken ct = default)
    {
        await permissions.EnsureAsync(performedByUserId, Permissions.AuditView, ct);
        return await auditLog.GetRecentAsync(count, ct);
    }

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
