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

    // Same query as GetActiveByPartyAsync but AsNoTracking: this is queried repeatedly against
    // the same long-lived DbContext by Blazor Server components on every ILanBoardNotifier
    // change (SeatGrid/Attendees/Tv), and without AsNoTracking, EF Core's change tracker
    // would keep returning the first-seen (now stale) Session instances instead of the fresh
    // row values written by other users' circuits or SessionPollingService's own DbContext.
    public async Task<IReadOnlyList<Session>> GetActiveByPartyForDisplayAsync(Guid partyId, CancellationToken ct = default)
    {
        var cutoff = Cutoff;
        return await db.Sessions
            .AsNoTracking()
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
