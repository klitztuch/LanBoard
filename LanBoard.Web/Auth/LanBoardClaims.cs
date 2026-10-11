using System.Security.Claims;

namespace LanBoard.Web.Auth;

public static class LanBoardClaims
{
    public const string UserId = "lanboard:userid";
    public const string Permission = "lanboard:permission";

    public static Guid? GetUserId(this ClaimsPrincipal principal)
        => Guid.TryParse(principal.FindFirstValue(UserId), out var id) ? id : null;
}
