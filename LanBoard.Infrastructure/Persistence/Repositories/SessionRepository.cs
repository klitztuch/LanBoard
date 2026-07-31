using LanBoard.Application.Interfaces;
using LanBoard.Core.Entities;
using LanBoard.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LanBoard.Infrastructure.Persistence.Repositories;

public class SessionRepository(AppDbContext db, IOptions<SessionTrackingConfiguration> options) : ISessionRepository
{
    private TimeSpan FreshnessWindow => TimeSpan.FromSeconds(options.Value.PollIntervalSeconds * 2);

    private DateTime Cutoff => DateTime.UtcNow - FreshnessWindow;

    public async Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Sessions.FindAsync([id], ct);

    public async Task<Session?> FindActiveAsync(Guid userId, Guid partyId, CancellationToken ct = default)
    {
        var cutoff = Cutoff;
        return await db.Sessions
            .Where(s => s.UserId == userId && s.PartyId == partyId && s.LastSeen >= cutoff)
            .OrderByDescending(s => s.LastSeen)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Session>> GetActiveByPartyAsync(Guid partyId, CancellationToken ct = default)
    {
        var cutoff = Cutoff;
        return await db.Sessions
            .Where(s => s.PartyId == partyId && s.LastSeen >= cutoff)
            .OrderByDescending(s => s.LastSeen)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Session entity, CancellationToken ct = default)
        => await db.Sessions.AddAsync(entity, ct);

    public void Remove(Session entity)
        => db.Sessions.Remove(entity);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
