using LanBoard.Application.Interfaces;
using LanBoard.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace LanBoard.Infrastructure.Persistence.Repositories;

public class AdminAuditLogRepository(AppDbContext db) : IAdminAuditLogRepository
{
    public async Task<AdminAuditLogEntry?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.AdminAuditLogEntries.FindAsync([id], ct);

    public async Task<IReadOnlyList<AdminAuditLogEntry>> GetRecentAsync(int count, CancellationToken ct = default)
        => await db.AdminAuditLogEntries
            .AsNoTracking()
            .Include(e => e.User)
            .OrderByDescending(e => e.CreatedAt)
            .Take(count)
            .ToListAsync(ct);

    public async Task AddAsync(AdminAuditLogEntry entity, CancellationToken ct = default)
        => await db.AdminAuditLogEntries.AddAsync(entity, ct);

    public void Remove(AdminAuditLogEntry entity)
        => db.AdminAuditLogEntries.Remove(entity);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
