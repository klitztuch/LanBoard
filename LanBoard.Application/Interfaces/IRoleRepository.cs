using LanBoard.Core.Entities;

namespace LanBoard.Application.Interfaces;

public interface IRoleRepository : IRepository<Role>
{
    Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default);
    Task<Role?> GetByNormalizedNameAsync(string normalizedName, CancellationToken ct = default);
    Task<IReadOnlyList<Role>> GetRolesForUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetUsersWithRolesAsync(CancellationToken ct = default);
    Task<bool> IsAssignedAsync(Guid userId, Guid roleId, CancellationToken ct = default);
    Task<int> CountUsersInRoleAsync(Guid roleId, CancellationToken ct = default);
    Task AddAssignmentAsync(UserRole assignment, CancellationToken ct = default);
    Task RemoveAssignmentAsync(Guid userId, Guid roleId, CancellationToken ct = default);
}
