using LanBoard.Application.Authorization;
using LanBoard.Application.Interfaces;
using LanBoard.Core.Entities;
using LanBoard.Infrastructure.Services;
using NSubstitute;

namespace LanBoard.Infrastructure.Tests.Services;

public class PermissionServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly IRoleRepository _roles = Substitute.For<IRoleRepository>();
    private readonly PermissionService _sut;

    public PermissionServiceTests()
    {
        _sut = new PermissionService(_roles);
    }

    private void GivenRoles(params Role[] roles)
        => _roles.GetRolesForUserAsync(UserId, Arg.Any<CancellationToken>()).Returns(roles);

    private static Role CreateRole(bool isSystem = false, params string[] permissions)
        => new() { Id = Guid.NewGuid(), Name = "Role", NormalizedName = "ROLE", IsSystem = isSystem, Permissions = [.. permissions] };

    [Fact]
    public async Task GetEffectivePermissionsAsync_MultipleRoles_ReturnsUnion()
    {
        GivenRoles(
            CreateRole(false, Permissions.TournamentManage),
            CreateRole(false, Permissions.CateringManage, Permissions.TournamentManage));

        var result = await _sut.GetEffectivePermissionsAsync(UserId);

        Assert.Equal([Permissions.CateringManage, Permissions.TournamentManage], result.Order());
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_SystemRole_ReturnsAllCatalogKeys()
    {
        GivenRoles(CreateRole(isSystem: true));

        var result = await _sut.GetEffectivePermissionsAsync(UserId);

        Assert.Equal(Permissions.All.Order(), result.Order());
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_UnknownKey_IsIgnored()
    {
        GivenRoles(CreateRole(false, "legacy.permission", Permissions.PartyManage));

        var result = await _sut.GetEffectivePermissionsAsync(UserId);

        Assert.Equal(Permissions.PartyManage, Assert.Single(result));
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_NoRoles_ReturnsEmpty()
    {
        GivenRoles();

        var result = await _sut.GetEffectivePermissionsAsync(UserId);

        Assert.Empty(result);
    }

    [Fact]
    public async Task EnsureAsync_PermissionMissing_Throws()
    {
        GivenRoles(CreateRole(false, Permissions.CateringManage));

        var ex = await Assert.ThrowsAsync<PermissionDeniedException>(() => _sut.EnsureAsync(UserId, Permissions.RolesManage));

        Assert.Equal(Permissions.RolesManage, ex.Permission);
    }

    [Fact]
    public async Task EnsureAsync_PermissionPresent_DoesNotThrow()
    {
        GivenRoles(CreateRole(false, Permissions.RolesManage));

        await _sut.EnsureAsync(UserId, Permissions.RolesManage);
    }
}
