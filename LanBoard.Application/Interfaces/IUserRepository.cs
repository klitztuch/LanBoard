using LanBoard.Core.Entities;

namespace LanBoard.Application.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> FindByProviderAsync(string provider, string providerUserId, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetAllWithIdentitiesAsync(CancellationToken ct = default);
}
