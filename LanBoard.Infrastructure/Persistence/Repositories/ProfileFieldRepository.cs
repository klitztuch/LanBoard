using LanBoard.Application.Interfaces;
using LanBoard.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace LanBoard.Infrastructure.Persistence.Repositories;

public class ProfileFieldRepository(AppDbContext db) : IProfileFieldRepository
{
    public async Task<ProfileFieldDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.ProfileFieldDefinitions.FindAsync([id], ct);

    public async Task<IReadOnlyList<ProfileFieldDefinition>> GetAllAsync(CancellationToken ct = default)
        => await db.ProfileFieldDefinitions
            .AsNoTracking()
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Key)
            .ToListAsync(ct);

    public async Task<bool> KeyExistsAsync(string key, CancellationToken ct = default)
        => await db.ProfileFieldDefinitions.AnyAsync(d => d.Key == key, ct);

    public async Task AddAsync(ProfileFieldDefinition entity, CancellationToken ct = default)
        => await db.ProfileFieldDefinitions.AddAsync(entity, ct);

    public void Remove(ProfileFieldDefinition entity)
        => db.ProfileFieldDefinitions.Remove(entity);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
