using LanBoard.Core.Entities;

namespace LanBoard.Application.Interfaces;

public interface ILanPartyRepository : IRepository<LanParty>
{
    Task<IReadOnlyList<LanParty>> GetAllAsync(CancellationToken ct = default);
    Task<LanParty?> GetActiveAsync(CancellationToken ct = default);
    Task<LanParty?> FindByInviteCodeAsync(string inviteCode, CancellationToken ct = default);
    Task<LanParty?> GetWithSeatsAndSessionsAsync(Guid id, CancellationToken ct = default);
}
