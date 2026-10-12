using LanBoard.Application.Authorization;
using LanBoard.Application.Errors;
using LanBoard.Application.Interfaces;
using LanBoard.Core.Entities;
using LanBoard.Infrastructure.Services;
using NSubstitute;

namespace LanBoard.Infrastructure.Tests.Services;

public class RoleServiceTests
{
    private static readonly Guid CallerId = Guid.NewGuid();

    private readonly IRoleRepository _roles = Substitute.For<IRoleRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPermissionService _permissions = Substitute.For<IPermissionService>();
    private readonly IAdminAuditLogRepository _auditLog = Substitute.For<IAdminAuditLogRepository>();
    private readonly RoleService _sut;

    public RoleServiceTests()
    {
        _sut = new RoleService(_roles, _users, _permissions, _auditLog);
        Grant(Permissions.RolesManage, Permissions.UsersManage);
    }

    private void Grant(params string[] granted)
        => _permissions.EnsureAsync(CallerId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => granted.Contains(ci.ArgAt<string>(1))
                ? Task.CompletedTask
                : Task.FromException(new PermissionDeniedException(ci.ArgAt<string>(1))));

    private Role GivenRole(string name = "Crew", bool isSystem = false, params string[] permissions)
    {
        var role = new Role
        {
            Id = isSystem ? Role.AdminId : Guid.NewGuid(),
            Name = name,
            NormalizedName = Role.Normalize(name),
            IsSystem = isSystem,
            Permissions = [.. permissions]
        };
        _roles.GetByIdAsync(role.Id, Arg.Any<CancellationToken>()).Returns(role);
        return role;
    }

    private User GivenUser(string name = "Bob")
    {
        var user = new User { Id = Guid.NewGuid(), DisplayName = name };
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    private Task AssertLoggedAsync(string action, Func<string, bool>? details = null)
        => _auditLog.Received(1).AddAsync(
            Arg.Is<AdminAuditLogEntry>(e => e.UserId == CallerId && e.Action == action && (details == null || details(e.Details!))),
            Arg.Any<CancellationToken>());

    // --- Create ---

    [Fact]
    public async Task CreateAsync_Valid_PersistsTrimmedRoleWithDistinctPermissionsAndLogs()
    {
        var role = await _sut.CreateAsync("  Crew ", [Permissions.TournamentManage, Permissions.TournamentManage], CallerId);

        Assert.Equal("Crew", role.Name);
        Assert.Equal("CREW", role.NormalizedName);
        Assert.Equal([Permissions.TournamentManage], role.Permissions);
        Assert.False(role.IsSystem);
        await _roles.Received(1).AddAsync(role, Arg.Any<CancellationToken>());
        await _roles.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await AssertLoggedAsync("RoleCreated", d => d.Contains("Crew") && d.Contains(Permissions.TournamentManage));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123456789012345678901234567890123456789012345678901")]
    public async Task CreateAsync_InvalidName_Throws(string name)
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.CreateAsync(name, [], CallerId));
        Assert.Equal(ErrorCodes.RoleNameInvalid, ex.Code);
        Assert.Equal([50], ex.Args);
        await _roles.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_DuplicateNameIgnoringCase_Throws()
    {
        _roles.GetByNormalizedNameAsync("CREW", Arg.Any<CancellationToken>())
            .Returns(new Role { Id = Guid.NewGuid(), Name = "Crew", NormalizedName = "CREW" });

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.CreateAsync("crew", [], CallerId));
        Assert.Equal(ErrorCodes.RoleNameTaken, ex.Code);

        await _roles.DidNotReceive().AddAsync(Arg.Any<Role>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_UnknownPermission_Throws()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.CreateAsync("Crew", ["nope.manage"], CallerId));
        Assert.Equal(ErrorCodes.RoleUnknownPermission, ex.Code);
        Assert.Equal(["nope.manage"], ex.Args);

        await _roles.DidNotReceive().AddAsync(Arg.Any<Role>(), Arg.Any<CancellationToken>());
    }

    // --- Update ---

    [Fact]
    public async Task UpdateAsync_Valid_UpdatesRoleAndLogsOldAndNewPermissions()
    {
        var role = GivenRole("Crew", false, Permissions.TournamentManage);

        await _sut.UpdateAsync(role.Id, "Catering", [Permissions.CateringManage], CallerId);

        Assert.Equal("Catering", role.Name);
        Assert.Equal("CATERING", role.NormalizedName);
        Assert.Equal([Permissions.CateringManage], role.Permissions);
        await _roles.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await AssertLoggedAsync("RoleUpdated", d => d.Contains(Permissions.TournamentManage) && d.Contains(Permissions.CateringManage));
    }

    [Fact]
    public async Task UpdateAsync_KeepsOwnName_DoesNotTreatAsDuplicate()
    {
        var role = GivenRole("Crew");
        _roles.GetByNormalizedNameAsync("CREW", Arg.Any<CancellationToken>()).Returns(role);

        await _sut.UpdateAsync(role.Id, "Crew", [Permissions.PartyManage], CallerId);

        await _roles.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_NameUsedByOtherRole_Throws()
    {
        var role = GivenRole("Crew");
        _roles.GetByNormalizedNameAsync("ORGA", Arg.Any<CancellationToken>())
            .Returns(new Role { Id = Guid.NewGuid(), Name = "Orga", NormalizedName = "ORGA" });

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.UpdateAsync(role.Id, "Orga", [], CallerId));
        Assert.Equal(ErrorCodes.RoleNameTaken, ex.Code);
        await _roles.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_SystemRole_Throws()
    {
        var admin = GivenRole("Admin", isSystem: true);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.UpdateAsync(admin.Id, "Admin", [], CallerId));
        Assert.Equal(ErrorCodes.RoleSystemImmutable, ex.Code);

        Assert.Empty(admin.Permissions);
        await _roles.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_RoleNotFound_Throws()
    {
        _roles.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Role?)null);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.UpdateAsync(Guid.NewGuid(), "Crew", [], CallerId));
        Assert.Equal(ErrorCodes.RoleNotFound, ex.Code);
    }

    // --- Delete ---

    [Fact]
    public async Task DeleteAsync_Valid_RemovesAndLogs()
    {
        var role = GivenRole("Crew");

        await _sut.DeleteAsync(role.Id, CallerId);

        _roles.Received(1).Remove(role);
        await _roles.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await AssertLoggedAsync("RoleDeleted", d => d.Contains("Crew"));
    }

    [Fact]
    public async Task DeleteAsync_SystemRole_Throws()
    {
        var admin = GivenRole("Admin", isSystem: true);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.DeleteAsync(admin.Id, CallerId));
        Assert.Equal(ErrorCodes.RoleSystemImmutable, ex.Code);

        _roles.DidNotReceive().Remove(Arg.Any<Role>());
    }

    // --- Assignments ---

    [Fact]
    public async Task GetUsersWithRolesAsync_MapsUsersWithTheirRoles()
    {
        var role = new Role { Id = Guid.NewGuid(), Name = "Crew", NormalizedName = "CREW" };
        var user = new User { Id = Guid.NewGuid(), DisplayName = "Bob", AvatarUrl = "a.png" };
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, Role = role });
        _roles.GetUsersWithRolesAsync(Arg.Any<CancellationToken>()).Returns([user]);

        var result = Assert.Single(await _sut.GetUsersWithRolesAsync(CallerId));

        Assert.Equal(user.Id, result.UserId);
        Assert.Equal("Bob", result.DisplayName);
        Assert.Equal("a.png", result.AvatarUrl);
        Assert.Same(role, Assert.Single(result.Roles));
    }

    [Fact]
    public async Task AssignAsync_NotAssigned_AddsAssignmentAndLogs()
    {
        var role = GivenRole("Crew");
        var user = GivenUser("Bob");

        await _sut.AssignAsync(user.Id, role.Id, CallerId);

        await _roles.Received(1).AddAssignmentAsync(
            Arg.Is<UserRole>(ur => ur.UserId == user.Id && ur.RoleId == role.Id), Arg.Any<CancellationToken>());
        await _roles.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await AssertLoggedAsync("RoleAssigned", d => d.Contains("Crew") && d.Contains("Bob"));
    }

    [Fact]
    public async Task AssignAsync_AlreadyAssigned_IsIdempotent()
    {
        var role = GivenRole("Crew");
        var user = GivenUser();
        _roles.IsAssignedAsync(user.Id, role.Id, Arg.Any<CancellationToken>()).Returns(true);

        await _sut.AssignAsync(user.Id, role.Id, CallerId);

        await _roles.DidNotReceive().AddAssignmentAsync(Arg.Any<UserRole>(), Arg.Any<CancellationToken>());
        await _auditLog.DidNotReceive().AddAsync(Arg.Any<AdminAuditLogEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignAsync_AdminRoleWithoutRolesManage_Throws()
    {
        Grant(Permissions.UsersManage);
        var admin = GivenRole("Admin", isSystem: true);
        var user = GivenUser();

        await Assert.ThrowsAsync<PermissionDeniedException>(() => _sut.AssignAsync(user.Id, admin.Id, CallerId));

        await _roles.DidNotReceive().AddAssignmentAsync(Arg.Any<UserRole>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignAsync_UserNotFound_Throws()
    {
        var role = GivenRole("Crew");
        _users.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.AssignAsync(Guid.NewGuid(), role.Id, CallerId));
        Assert.Equal(ErrorCodes.UserNotFound, ex.Code);
    }

    [Fact]
    public async Task UnassignAsync_Assigned_RemovesAssignmentAndLogs()
    {
        var role = GivenRole("Crew");
        var user = GivenUser("Bob");
        _roles.IsAssignedAsync(user.Id, role.Id, Arg.Any<CancellationToken>()).Returns(true);

        await _sut.UnassignAsync(user.Id, role.Id, CallerId);

        await _roles.Received(1).RemoveAssignmentAsync(user.Id, role.Id, Arg.Any<CancellationToken>());
        await _roles.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await AssertLoggedAsync("RoleUnassigned", d => d.Contains("Crew") && d.Contains("Bob"));
    }

    [Fact]
    public async Task UnassignAsync_LastAdmin_Throws()
    {
        var admin = GivenRole("Admin", isSystem: true);
        var user = GivenUser();
        _roles.IsAssignedAsync(user.Id, admin.Id, Arg.Any<CancellationToken>()).Returns(true);
        _roles.CountUsersInRoleAsync(admin.Id, Arg.Any<CancellationToken>()).Returns(1);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.UnassignAsync(user.Id, admin.Id, CallerId));
        Assert.Equal(ErrorCodes.RoleLastAdmin, ex.Code);

        await _roles.DidNotReceive().RemoveAssignmentAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnassignAsync_AdminWithOtherHolders_Removes()
    {
        var admin = GivenRole("Admin", isSystem: true);
        var user = GivenUser();
        _roles.IsAssignedAsync(user.Id, admin.Id, Arg.Any<CancellationToken>()).Returns(true);
        _roles.CountUsersInRoleAsync(admin.Id, Arg.Any<CancellationToken>()).Returns(2);

        await _sut.UnassignAsync(user.Id, admin.Id, CallerId);

        await _roles.Received(1).RemoveAssignmentAsync(user.Id, admin.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnassignAsync_AdminRoleWithoutRolesManage_Throws()
    {
        Grant(Permissions.UsersManage);
        var admin = GivenRole("Admin", isSystem: true);
        var user = GivenUser();
        _roles.IsAssignedAsync(user.Id, admin.Id, Arg.Any<CancellationToken>()).Returns(true);
        _roles.CountUsersInRoleAsync(admin.Id, Arg.Any<CancellationToken>()).Returns(2);

        await Assert.ThrowsAsync<PermissionDeniedException>(() => _sut.UnassignAsync(user.Id, admin.Id, CallerId));

        await _roles.DidNotReceive().RemoveAssignmentAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureAssignedByNameAsync_AssignsMissingKnownRolesWithoutCheckOrAudit()
    {
        var userId = Guid.NewGuid();
        var admin = new Role { Id = Role.AdminId, Name = "Admin", NormalizedName = "ADMIN", IsSystem = true };
        _roles.GetByNormalizedNameAsync("ADMIN", Arg.Any<CancellationToken>()).Returns(admin);

        await _sut.EnsureAssignedByNameAsync(userId, ["admin", "Unknown"]);

        await _roles.Received(1).AddAssignmentAsync(
            Arg.Is<UserRole>(ur => ur.UserId == userId && ur.RoleId == Role.AdminId), Arg.Any<CancellationToken>());
        await _roles.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _permissions.DidNotReceive().EnsureAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _auditLog.DidNotReceive().AddAsync(Arg.Any<AdminAuditLogEntry>(), Arg.Any<CancellationToken>());
    }

    // --- Permission denied: nothing persisted ---

    public static TheoryData<string, string> DeniedCases => new()
    {
        { "Create", Permissions.RolesManage },
        { "Update", Permissions.RolesManage },
        { "Delete", Permissions.RolesManage },
        { "Assign", Permissions.UsersManage },
        { "Unassign", Permissions.UsersManage },
    };

    [Theory]
    [MemberData(nameof(DeniedCases))]
    public async Task MutatingMethod_CallerLacksPermission_ThrowsAndDoesNotSave(string method, string requiredPermission)
    {
        Grant(Permissions.All.Where(k => k != requiredPermission).ToArray());
        var role = GivenRole("Crew");
        var user = GivenUser();
        _roles.IsAssignedAsync(user.Id, role.Id, Arg.Any<CancellationToken>()).Returns(method == "Unassign");

        Func<Task> act = method switch
        {
            "Create" => () => _sut.CreateAsync("New", [], CallerId),
            "Update" => () => _sut.UpdateAsync(role.Id, "New", [], CallerId),
            "Delete" => () => _sut.DeleteAsync(role.Id, CallerId),
            "Assign" => () => _sut.AssignAsync(user.Id, role.Id, CallerId),
            "Unassign" => () => _sut.UnassignAsync(user.Id, role.Id, CallerId),
            _ => throw new ArgumentOutOfRangeException(nameof(method))
        };

        var ex = await Assert.ThrowsAsync<PermissionDeniedException>(act);

        Assert.Equal(requiredPermission, ex.Permission);
        await _roles.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditLog.DidNotReceive().AddAsync(Arg.Any<AdminAuditLogEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetUsersWithRolesAsync_CallerLacksUsersManage_Throws()
    {
        Grant(Permissions.RolesManage);

        await Assert.ThrowsAsync<PermissionDeniedException>(() => _sut.GetUsersWithRolesAsync(CallerId));
    }
}
