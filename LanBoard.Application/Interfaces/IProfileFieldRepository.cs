using LanBoard.Core.Entities;

namespace LanBoard.Application.Interfaces;

public interface IProfileFieldRepository : IRepository<ProfileFieldDefinition>
{
    /// Read-only, ordered by SortOrder, then Key.
    Task<IReadOnlyList<ProfileFieldDefinition>> GetAllAsync(CancellationToken ct = default);
    Task<bool> KeyExistsAsync(string key, CancellationToken ct = default);
}
