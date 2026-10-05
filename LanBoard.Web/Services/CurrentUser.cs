using LanBoard.Application.Users;
using LanBoard.Web.Extensions;
using Microsoft.AspNetCore.Components.Authorization;

namespace LanBoard.Web.Services;

public sealed class CurrentUser(AuthenticationStateProvider auth) : ICurrentUser
{
    public async Task<bool> IsAdminAsync(CancellationToken ct = default)
    {
        var state = await auth.GetAuthenticationStateAsync();
        return state.User.Identity?.IsAuthenticated == true
            && state.User.HasClaim(AdminClaim.Type, AdminClaim.Value);
    }
}
