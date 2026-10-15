using LanBoard.Core.Entities;

namespace LanBoard.Application.Interfaces;

public interface ILocationRepository : IRepository<Location>
{
    Task<IReadOnlyList<Location>> GetAllAsync(CancellationToken ct = default);
    Task<Location?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<int> CountPartiesAsync(Guid locationId, CancellationToken ct = default);
}
