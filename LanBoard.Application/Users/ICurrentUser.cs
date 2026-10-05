namespace LanBoard.Application.Users;

public interface ICurrentUser
{
    /// True when the current principal is authenticated and has the admin claim.
    Task<bool> IsAdminAsync(CancellationToken ct = default);
}
