using LanBoard.Application.Interfaces;
using LanBoard.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace LanBoard.Infrastructure.Persistence.Repositories;

public class RsvpRepository(AppDbContext db) : IRsvpRepository
{
    public async Task<Rsvp?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Rsvps.FindAsync([id], ct);

    public async Task<Rsvp?> GetByUserAndPartyAsync(Guid userId, Guid partyId, CancellationToken ct = default)
        => await db.Rsvps.FirstOrDefaultAsync(r => r.UserId == userId && r.PartyId == partyId, ct);

    public async Task<IReadOnlyList<Rsvp>> GetByPartyAsync(Guid partyId, CancellationToken ct = default)
        => await db.Rsvps.Where(r => r.PartyId == partyId).ToListAsync(ct);

    public async Task AddAsync(Rsvp entity, CancellationToken ct = default)
        => await db.Rsvps.AddAsync(entity, ct);

    public void Remove(Rsvp entity)
        => db.Rsvps.Remove(entity);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
