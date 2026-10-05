using LanBoard.Application.Interfaces;
using LanBoard.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace LanBoard.Infrastructure.Persistence.Repositories;

public class TournamentRepository(AppDbContext db) : ITournamentRepository
{
    public async Task<Tournament?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Tournaments.FindAsync([id], ct);

    public async Task<IReadOnlyList<Tournament>> GetByPartyAsync(Guid partyId, CancellationToken ct = default)
        => await db.Tournaments
            .AsNoTracking()
            .Where(t => t.PartyId == partyId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task<Tournament?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
        => await db.Tournaments
            .Include(t => t.Participants).ThenInclude(p => p.User)
            .Include(t => t.Matches).ThenInclude(m => m.Participant1).ThenInclude(p => p!.User)
            .Include(t => t.Matches).ThenInclude(m => m.Participant2).ThenInclude(p => p!.User)
            .Include(t => t.Matches).ThenInclude(m => m.Winner).ThenInclude(p => p!.User)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task AddAsync(Tournament entity, CancellationToken ct = default)
        => await db.Tournaments.AddAsync(entity, ct);

    public void Remove(Tournament entity)
        => db.Tournaments.Remove(entity);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
