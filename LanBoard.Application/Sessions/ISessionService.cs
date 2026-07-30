using LanBoard.Core.Entities;

namespace LanBoard.Application.Sessions;

public interface ISessionService
{
    Task SyncActiveSessionsAsync(Guid partyId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, Session>> GetActiveSessionsByPartyAsync(Guid partyId, CancellationToken ct = default);
}
