using LanBoard.Application.Interfaces;
using LanBoard.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace LanBoard.Infrastructure.Persistence.Repositories;

public class SeatRepository(AppDbContext db) : ISeatRepository
{
    public async Task<Seat?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Seats.FindAsync([id], ct);

    public async Task<IReadOnlyList<Seat>> GetByPartyAsync(Guid partyId, CancellationToken ct = default)
        => await db.Seats
            .AsNoTracking()
            .Where(s => s.PartyId == partyId)
            .Include(s => s.AssignedUser)
            .OrderBy(s => s.Label)
            .ToListAsync(ct);

    public async Task<Seat?> GetByUserAsync(Guid partyId, Guid userId, CancellationToken ct = default)
        => await db.Seats
            .FirstOrDefaultAsync(s => s.PartyId == partyId && s.AssignedUserId == userId, ct);

    public async Task AddAsync(Seat entity, CancellationToken ct = default)
        => await db.Seats.AddAsync(entity, ct);

    public void Remove(Seat entity)
        => db.Seats.Remove(entity);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
