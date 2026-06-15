using LanBoard.Application.Interfaces;
using LanBoard.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace LanBoard.Infrastructure.Persistence.Repositories;

public class LanPartyRepository(AppDbContext db) : ILanPartyRepository
{
    public async Task<LanParty?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.LanParties.FindAsync([id], ct);

    public async Task<LanParty?> GetActiveAsync(CancellationToken ct = default)
        => await db.LanParties.OrderByDescending(p => p.Date).FirstOrDefaultAsync(ct);

    public async Task<LanParty?> FindByInviteCodeAsync(string inviteCode, CancellationToken ct = default)
        => await db.LanParties.FirstOrDefaultAsync(p => p.InviteCode == inviteCode, ct);

    public async Task<LanParty?> GetWithSeatsAndSessionsAsync(Guid id, CancellationToken ct = default)
        => await db.LanParties
            .Include(p => p.Seats).ThenInclude(s => s.AssignedUser)
            .Include(p => p.Sessions)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task AddAsync(LanParty entity, CancellationToken ct = default)
        => await db.LanParties.AddAsync(entity, ct);

    public void Remove(LanParty entity)
        => db.LanParties.Remove(entity);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
