using System.Security.Claims;
using LanBoard.Application.Authorization;
using Microsoft.AspNetCore.Authentication;

namespace LanBoard.Web.Auth;

/// <summary>
/// Resolves permissions from the database on every request instead of storing them in the cookie,
/// so role changes take effect on the user's next page load without re-login.
/// </summary>
public class PermissionClaimsTransformation(IPermissionService permissionService) : IClaimsTransformation
{
    /// <summary>Authentication type of the per-request permission identity; never part of the cookie.</summary>
    public const string AuthenticationType = "lanboard:permissions";

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // TransformAsync may run several times per request.
        if (principal.Identities.Any(i => i.AuthenticationType == AuthenticationType))
            return principal;
        if (principal.GetUserId() is not { } userId)
            return principal;

        var permissions = await permissionService.GetEffectivePermissionsAsync(userId);

        var clone = principal.Clone();
        clone.AddIdentity(new ClaimsIdentity(
            permissions.Select(p => new Claim(LanBoardClaims.Permission, p)),
            AuthenticationType));
        return clone;
    }
}
