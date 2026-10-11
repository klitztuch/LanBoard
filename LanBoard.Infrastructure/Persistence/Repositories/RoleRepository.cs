using LanBoard.Application.Interfaces;
using LanBoard.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace LanBoard.Infrastructure.Persistence.Repositories;

public class RoleRepository(AppDbContext db) : IRoleRepository
{
    public async Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Roles.FindAsync([id], ct);

    public async Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default)
        => await db.Roles
            .AsNoTracking()
            .OrderByDescending(r => r.IsSystem)
            .ThenBy(r => r.Name)
            .ToListAsync(ct);

    public async Task<Role?> GetByNormalizedNameAsync(string normalizedName, CancellationToken ct = default)
        => await db.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.NormalizedName == normalizedName, ct);

    public async Task<IReadOnlyList<Role>> GetRolesForUserAsync(Guid userId, CancellationToken ct = default)
        => await db.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<User>> GetUsersWithRolesAsync(CancellationToken ct = default)
        => await db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .OrderBy(u => u.DisplayName)
            .ToListAsync(ct);

    public async Task<bool> IsAssignedAsync(Guid userId, Guid roleId, CancellationToken ct = default)
        => await db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId, ct);

    public async Task<int> CountUsersInRoleAsync(Guid roleId, CancellationToken ct = default)
        => await db.UserRoles.CountAsync(ur => ur.RoleId == roleId, ct);

    public async Task AddAssignmentAsync(UserRole assignment, CancellationToken ct = default)
        => await db.UserRoles.AddAsync(assignment, ct);

    public async Task RemoveAssignmentAsync(Guid userId, Guid roleId, CancellationToken ct = default)
    {
        var assignment = await db.UserRoles.FindAsync([userId, roleId], ct);
        if (assignment is not null)
            db.UserRoles.Remove(assignment);
    }

    public async Task AddAsync(Role entity, CancellationToken ct = default)
        => await db.Roles.AddAsync(entity, ct);

    public void Remove(Role entity)
        => db.Roles.Remove(entity);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
