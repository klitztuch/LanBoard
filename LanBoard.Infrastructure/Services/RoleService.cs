using LanBoard.Application.Authorization;
using LanBoard.Application.Interfaces;
using LanBoard.Application.Roles;
using LanBoard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LanBoard.Infrastructure.Services;

public class RoleService(
    IRoleRepository roles,
    IUserRepository users,
    IPermissionService permissionService,
    IAdminAuditLogRepository auditLog) : IRoleService
{
    private const int MaxNameLength = 50;
    private const string DuplicateNameMessage = "Eine Rolle mit diesem Namen existiert bereits.";
    private const string SystemRoleMessage = "Die Admin-Rolle kann nicht geändert oder gelöscht werden.";

    public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default)
        => roles.GetAllAsync(ct);

    public async Task<Role> CreateAsync(string name, IReadOnlyCollection<string> permissions, Guid performedByUserId, CancellationToken ct = default)
    {
        await permissionService.EnsureAsync(performedByUserId, Permissions.RolesManage, ct);

        var trimmedName = ValidateName(name);
        var normalizedName = Role.Normalize(trimmedName);
        var keys = ValidatePermissions(permissions);
        if (await roles.GetByNormalizedNameAsync(normalizedName, ct) is not null)
            throw new InvalidOperationException(DuplicateNameMessage);

        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            NormalizedName = normalizedName,
            Permissions = keys,
            CreatedAt = DateTime.UtcNow
        };
        await roles.AddAsync(role, ct);
        await SaveMappingDuplicateNameAsync(ct);
        await LogAsync(performedByUserId, "RoleCreated", $"Rolle \"{role.Name}\" erstellt ({FormatPermissions(keys)})", ct);
        return role;
    }

    public async Task UpdateAsync(Guid roleId, string name, IReadOnlyCollection<string> permissions, Guid performedByUserId, CancellationToken ct = default)
    {
        await permissionService.EnsureAsync(performedByUserId, Permissions.RolesManage, ct);

        var role = await roles.GetByIdAsync(roleId, ct)
            ?? throw new InvalidOperationException("Rolle nicht gefunden.");
        if (role.IsSystem)
            throw new InvalidOperationException(SystemRoleMessage);

        var trimmedName = ValidateName(name);
        var normalizedName = Role.Normalize(trimmedName);
        var keys = ValidatePermissions(permissions);
        if (await roles.GetByNormalizedNameAsync(normalizedName, ct) is { } existing && existing.Id != roleId)
            throw new InvalidOperationException(DuplicateNameMessage);

        var oldName = role.Name;
        var oldKeys = role.Permissions;
        role.Name = trimmedName;
        role.NormalizedName = normalizedName;
        role.Permissions = keys;
        await SaveMappingDuplicateNameAsync(ct);

        var renamed = oldName == trimmedName ? "" : $" (vorher \"{oldName}\")";
        await LogAsync(performedByUserId, "RoleUpdated",
            $"Rolle \"{trimmedName}\"{renamed} bearbeitet: {FormatPermissions(oldKeys)} → {FormatPermissions(keys)}", ct);
    }

    public async Task DeleteAsync(Guid roleId, Guid performedByUserId, CancellationToken ct = default)
    {
        await permissionService.EnsureAsync(performedByUserId, Permissions.RolesManage, ct);

        var role = await roles.GetByIdAsync(roleId, ct)
            ?? throw new InvalidOperationException("Rolle nicht gefunden.");
        if (role.IsSystem)
            throw new InvalidOperationException(SystemRoleMessage);

        // Assignments are removed by the cascading FK on UserRoles.RoleId.
        roles.Remove(role);
        await roles.SaveChangesAsync(ct);
        await LogAsync(performedByUserId, "RoleDeleted", $"Rolle \"{role.Name}\" gelöscht ({FormatPermissions(role.Permissions)})", ct);
    }

    public async Task<IReadOnlyList<UserWithRoles>> GetUsersWithRolesAsync(Guid performedByUserId, CancellationToken ct = default)
    {
        await permissionService.EnsureAsync(performedByUserId, Permissions.UsersManage, ct);

        var result = await roles.GetUsersWithRolesAsync(ct);
        return result
            .Select(u => new UserWithRoles(
                u.Id,
                u.DisplayName,
                u.AvatarUrl,
                u.UserRoles.Select(ur => ur.Role).OrderByDescending(r => r.IsSystem).ThenBy(r => r.Name).ToList()))
            .ToList();
    }

    public async Task AssignAsync(Guid userId, Guid roleId, Guid performedByUserId, CancellationToken ct = default)
    {
        await permissionService.EnsureAsync(performedByUserId, Permissions.UsersManage, ct);

        var role = await roles.GetByIdAsync(roleId, ct)
            ?? throw new InvalidOperationException("Rolle nicht gefunden.");
        if (role.IsSystem)
            await permissionService.EnsureAsync(performedByUserId, Permissions.RolesManage, ct);
        var user = await users.GetByIdAsync(userId, ct)
            ?? throw new InvalidOperationException("Benutzer nicht gefunden.");

        if (await roles.IsAssignedAsync(userId, roleId, ct))
            return;

        await roles.AddAssignmentAsync(new UserRole { UserId = userId, RoleId = roleId, AssignedAt = DateTime.UtcNow }, ct);
        await roles.SaveChangesAsync(ct);
        await LogAsync(performedByUserId, "RoleAssigned", $"Rolle \"{role.Name}\" an {user.DisplayName} vergeben", ct);
    }

    public async Task UnassignAsync(Guid userId, Guid roleId, Guid performedByUserId, CancellationToken ct = default)
    {
        await permissionService.EnsureAsync(performedByUserId, Permissions.UsersManage, ct);

        var role = await roles.GetByIdAsync(roleId, ct)
            ?? throw new InvalidOperationException("Rolle nicht gefunden.");
        if (role.IsSystem)
            await permissionService.EnsureAsync(performedByUserId, Permissions.RolesManage, ct);
        var user = await users.GetByIdAsync(userId, ct)
            ?? throw new InvalidOperationException("Benutzer nicht gefunden.");

        if (!await roles.IsAssignedAsync(userId, roleId, ct))
            return;

        // Check-then-act: two admins removing each other concurrently is an accepted race for a LAN tool.
        if (role.IsSystem && await roles.CountUsersInRoleAsync(roleId, ct) <= 1)
            throw new InvalidOperationException("Mindestens ein Benutzer muss die Admin-Rolle behalten.");

        await roles.RemoveAssignmentAsync(userId, roleId, ct);
        await roles.SaveChangesAsync(ct);
        await LogAsync(performedByUserId, "RoleUnassigned", $"Rolle \"{role.Name}\" von {user.DisplayName} entfernt", ct);
    }

    public async Task EnsureAssignedByNameAsync(Guid userId, IEnumerable<string> roleNames, CancellationToken ct = default)
    {
        var changed = false;
        foreach (var name in roleNames)
        {
            var role = await roles.GetByNormalizedNameAsync(Role.Normalize(name), ct);
            if (role is null || await roles.IsAssignedAsync(userId, role.Id, ct))
                continue;

            await roles.AddAssignmentAsync(new UserRole { UserId = userId, RoleId = role.Id, AssignedAt = DateTime.UtcNow }, ct);
            changed = true;
        }

        if (changed)
            await roles.SaveChangesAsync(ct);
    }

    private static string ValidateName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length is 0 or > MaxNameLength)
            throw new InvalidOperationException($"Name muss 1–{MaxNameLength} Zeichen lang sein.");
        return trimmed;
    }

    private static List<string> ValidatePermissions(IEnumerable<string> keys)
    {
        var distinct = keys.Distinct().ToList();
        if (distinct.FirstOrDefault(k => !Permissions.All.ContainsKey(k)) is { } unknown)
            throw new InvalidOperationException($"Unbekannte Berechtigung: {unknown}");
        return distinct;
    }

    private async Task SaveMappingDuplicateNameAsync(CancellationToken ct)
    {
        try
        {
            await roles.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new InvalidOperationException(DuplicateNameMessage, ex);
        }
    }

    private static string FormatPermissions(IEnumerable<string> keys)
        => keys.Any() ? string.Join(", ", keys.Order()) : "keine Berechtigungen";

    private async Task LogAsync(Guid userId, string action, string details, CancellationToken ct)
    {
        await auditLog.AddAsync(new AdminAuditLogEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            Details = details,
            CreatedAt = DateTime.UtcNow
        }, ct);
        await auditLog.SaveChangesAsync(ct);
    }
}
