namespace LanBoard.Application.Authorization;

public interface IPermissionService
{
    Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct = default);
    Task<bool> HasAsync(Guid userId, string permission, CancellationToken ct = default);

    /// <summary>Throws <see cref="PermissionDeniedException"/> if the user lacks the permission.</summary>
    Task EnsureAsync(Guid userId, string permission, CancellationToken ct = default);
}
