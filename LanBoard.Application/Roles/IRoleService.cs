using LanBoard.Core.Entities;

namespace LanBoard.Application.Roles;

public interface IRoleService
{
    Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default);
    Task<Role> CreateAsync(string name, IReadOnlyCollection<string> permissions, Guid performedByUserId, CancellationToken ct = default);
    Task UpdateAsync(Guid roleId, string name, IReadOnlyCollection<string> permissions, Guid performedByUserId, CancellationToken ct = default);
    Task DeleteAsync(Guid roleId, Guid performedByUserId, CancellationToken ct = default);

    Task<IReadOnlyList<UserWithRoles>> GetUsersWithRolesAsync(Guid performedByUserId, CancellationToken ct = default);
    Task AssignAsync(Guid userId, Guid roleId, Guid performedByUserId, CancellationToken ct = default);
    Task UnassignAsync(Guid userId, Guid roleId, Guid performedByUserId, CancellationToken ct = default);

    /// <summary>Dev only: ensures the named roles are assigned. No permission check, no audit entry.</summary>
    Task EnsureAssignedByNameAsync(Guid userId, IEnumerable<string> roleNames, CancellationToken ct = default);
}

public record UserWithRoles(Guid UserId, string DisplayName, string? AvatarUrl, IReadOnlyList<Role> Roles);
