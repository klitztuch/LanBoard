using LanBoard.Application.Interfaces;
using LanBoard.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace LanBoard.Infrastructure.Persistence.Repositories;

public class PartyMembershipRepository(AppDbContext db) : IPartyMembershipRepository
{
    public async Task<PartyMembership?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.PartyMemberships.FindAsync([id], ct);

    public async Task<bool> ExistsAsync(Guid userId, Guid partyId, CancellationToken ct = default)
        => await db.PartyMemberships.AnyAsync(m => m.UserId == userId && m.PartyId == partyId, ct);

    public async Task AddAsync(PartyMembership entity, CancellationToken ct = default)
        => await db.PartyMemberships.AddAsync(entity, ct);

    public void Remove(PartyMembership entity)
        => db.PartyMemberships.Remove(entity);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
