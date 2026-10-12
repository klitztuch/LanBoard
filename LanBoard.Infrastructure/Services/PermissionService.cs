using LanBoard.Application.Authorization;
using LanBoard.Application.Interfaces;

namespace LanBoard.Infrastructure.Services;

public class PermissionService(IRoleRepository roles) : IPermissionService
{
    public async Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct = default)
    {
        var userRoles = await roles.GetRolesForUserAsync(userId, ct);

        // The system role resolves to the full catalog at runtime, so new permissions apply to admins without a data migration.
        if (userRoles.Any(r => r.IsSystem))
            return Permissions.All.ToHashSet();

        return userRoles
            .SelectMany(r => r.Permissions)
            .Where(Permissions.All.Contains)
            .ToHashSet();
    }

    public async Task<bool> HasAsync(Guid userId, string permission, CancellationToken ct = default)
        => (await GetEffectivePermissionsAsync(userId, ct)).Contains(permission);

    public async Task EnsureAsync(Guid userId, string permission, CancellationToken ct = default)
    {
        if (!await HasAsync(userId, permission, ct))
            throw new PermissionDeniedException(permission);
    }
}
