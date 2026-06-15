using LanBoard.Application.Interfaces;
using LanBoard.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace LanBoard.Infrastructure.Persistence.Repositories;

public class UserRepository(AppDbContext db) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Users.FindAsync([id], ct);

    public async Task<User?> FindByProviderAsync(string provider, string providerUserId, CancellationToken ct = default)
        => await db.UserIdentities
            .Where(i => i.Provider == provider && i.ProviderUserId == providerUserId)
            .Select(i => i.User)
            .FirstOrDefaultAsync(ct);

    public async Task AddAsync(User entity, CancellationToken ct = default)
        => await db.Users.AddAsync(entity, ct);

    public void Remove(User entity)
        => db.Users.Remove(entity);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
