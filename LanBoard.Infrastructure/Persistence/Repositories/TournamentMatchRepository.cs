using LanBoard.Application.Interfaces;
using LanBoard.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace LanBoard.Infrastructure.Persistence.Repositories;

public class TournamentMatchRepository(AppDbContext db) : ITournamentMatchRepository
{
    public async Task<TournamentMatch?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.TournamentMatches.FindAsync([id], ct);

    public async Task<TournamentMatch?> FindAsync(Guid tournamentId, int round, int slot, CancellationToken ct = default)
        => await db.TournamentMatches
            .FirstOrDefaultAsync(m => m.TournamentId == tournamentId && m.Round == round && m.Slot == slot, ct);

    public async Task AddAsync(TournamentMatch entity, CancellationToken ct = default)
        => await db.TournamentMatches.AddAsync(entity, ct);

    public void Remove(TournamentMatch entity)
        => db.TournamentMatches.Remove(entity);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
