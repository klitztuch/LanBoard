using LanBoard.Core.Entities;

namespace LanBoard.Application.Interfaces;

public interface IAdminAuditLogRepository : IRepository<AdminAuditLogEntry>
{
    Task<IReadOnlyList<AdminAuditLogEntry>> GetRecentAsync(int count, CancellationToken ct = default);
}
