using LanBoard.Core.Entities;

namespace LanBoard.Application.Interfaces;

public interface ISessionRepository : IRepository<Session>
{
    Task<Session?> FindActiveAsync(Guid userId, Guid partyId, CancellationToken ct = default);
    Task<IReadOnlyList<Session>> GetActiveByPartyAsync(Guid partyId, CancellationToken ct = default);
    Task<IReadOnlyList<Session>> GetActiveByPartyForDisplayAsync(Guid partyId, CancellationToken ct = default);
}
