using LanBoard.Application.Interfaces;
using LanBoard.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace LanBoard.Infrastructure.Persistence.Repositories;

public class TournamentParticipantRepository(AppDbContext db) : ITournamentParticipantRepository
{
    public async Task<TournamentParticipant?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.TournamentParticipants.FindAsync([id], ct);

    public async Task<IReadOnlyList<TournamentParticipant>> GetByTournamentAsync(Guid tournamentId, CancellationToken ct = default)
        => await db.TournamentParticipants
            .Where(p => p.TournamentId == tournamentId)
            .Include(p => p.User)
            .ToListAsync(ct);

    public async Task<TournamentParticipant?> FindAsync(Guid tournamentId, Guid userId, CancellationToken ct = default)
        => await db.TournamentParticipants
            .FirstOrDefaultAsync(p => p.TournamentId == tournamentId && p.UserId == userId, ct);

    public async Task AddAsync(TournamentParticipant entity, CancellationToken ct = default)
        => await db.TournamentParticipants.AddAsync(entity, ct);

    public void Remove(TournamentParticipant entity)
        => db.TournamentParticipants.Remove(entity);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
