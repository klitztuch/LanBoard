using LanBoard.Application.Interfaces;
using LanBoard.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace LanBoard.Infrastructure.Persistence.Repositories;

public class LocationRepository(AppDbContext db) : ILocationRepository
{
    public async Task<Location?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Locations.FindAsync([id], ct);

    public async Task<IReadOnlyList<Location>> GetAllAsync(CancellationToken ct = default)
        => await db.Locations
            .AsNoTracking()
            .OrderBy(l => l.Name)
            .ToListAsync(ct);

    public async Task<Location?> GetByNameAsync(string name, CancellationToken ct = default)
        => await db.Locations
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Name == name, ct);

    public async Task<int> CountPartiesAsync(Guid locationId, CancellationToken ct = default)
        => await db.LanParties.CountAsync(p => p.LocationId == locationId, ct);

    public async Task AddAsync(Location entity, CancellationToken ct = default)
        => await db.Locations.AddAsync(entity, ct);

    public void Remove(Location entity)
        => db.Locations.Remove(entity);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
