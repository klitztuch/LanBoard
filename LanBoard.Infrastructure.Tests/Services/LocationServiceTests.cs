using LanBoard.Application.Admin;
using LanBoard.Application.Authorization;
using LanBoard.Application.Errors;
using LanBoard.Application.Interfaces;
using LanBoard.Application.Locations;
using LanBoard.Core.Entities;
using LanBoard.Infrastructure.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace LanBoard.Infrastructure.Tests.Services;

public class LocationServiceTests
{
    private readonly ILocationRepository _locations = Substitute.For<ILocationRepository>();
    private readonly IAdminAuditLogRepository _auditLog = Substitute.For<IAdminAuditLogRepository>();
    private readonly IPermissionService _permissions = Substitute.For<IPermissionService>();
    private readonly LocationService _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public LocationServiceTests()
    {
        _sut = new LocationService(_locations, _auditLog, _permissions);
    }

    [Fact]
    public async Task CreateAsync_WithoutPermission_ThrowsBeforeSaving()
    {
        _permissions.EnsureAsync(_userId, Permissions.PartyManage, Arg.Any<CancellationToken>())
            .ThrowsAsync(new PermissionDeniedException(Permissions.PartyManage));

        await Assert.ThrowsAsync<PermissionDeniedException>(() => _sut.CreateAsync(new LocationInput("Garage"), _userId));

        await _locations.DidNotReceive().AddAsync(Arg.Any<Location>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_TrimsAndNullsBlankTexts_PersistsAndLogs()
    {
        var input = new LocationInput("  Garage  ", "Main St 1\n12345 Town  ", 52.5, 13.4, "   ", "Backyard", "");

        var location = await _sut.CreateAsync(input, _userId);

        Assert.Equal("Garage", location.Name);
        Assert.Equal("Main St 1\n12345 Town", location.Address);
        Assert.Equal(52.5, location.Latitude);
        Assert.Equal(13.4, location.Longitude);
        Assert.Null(location.Directions);
        Assert.Equal("Backyard", location.Parking);
        Assert.Null(location.Notes);
        await _locations.Received(1).AddAsync(location, Arg.Any<CancellationToken>());
        await _locations.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditLog.Received(1).AddAsync(
            Arg.Is<AdminAuditLogEntry>(e => e.UserId == _userId && e.Action == AuditActions.LocationCreated && e.Details!.Contains("Garage")),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_BlankName_Throws(string name)
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.CreateAsync(new LocationInput(name), _userId));

        Assert.Equal(ErrorCodes.LocationNameInvalid, ex.Code);
    }

    [Fact]
    public async Task CreateAsync_NameTooLong_Throws()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.CreateAsync(new LocationInput(new string('x', 201)), _userId));

        Assert.Equal(ErrorCodes.LocationNameInvalid, ex.Code);
    }

    [Fact]
    public async Task CreateAsync_TextTooLong_Throws()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _sut.CreateAsync(new LocationInput("Garage", Notes: new string('x', 4001)), _userId));

        Assert.Equal(ErrorCodes.LocationTextTooLong, ex.Code);
    }

    [Theory]
    [InlineData(52.5, null)]
    [InlineData(null, 13.4)]
    [InlineData(91.0, 13.4)]
    [InlineData(-91.0, 13.4)]
    [InlineData(52.5, 181.0)]
    [InlineData(52.5, -181.0)]
    public async Task CreateAsync_InvalidCoordinates_Throws(double? latitude, double? longitude)
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _sut.CreateAsync(new LocationInput("Garage", Latitude: latitude, Longitude: longitude), _userId));

        Assert.Equal(ErrorCodes.LocationCoordinatesInvalid, ex.Code);
    }

    [Fact]
    public async Task CreateAsync_NameTaken_Throws()
    {
        _locations.GetByNameAsync("Garage", Arg.Any<CancellationToken>())
            .Returns(new Location { Id = Guid.NewGuid(), Name = "Garage" });

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.CreateAsync(new LocationInput("Garage"), _userId));

        Assert.Equal(ErrorCodes.LocationNameTaken, ex.Code);
        await _locations.DidNotReceive().AddAsync(Arg.Any<Location>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_NameDiffersOnlyInCase_IsAllowed()
    {
        _locations.GetByNameAsync("Garage", Arg.Any<CancellationToken>())
            .Returns(new Location { Id = Guid.NewGuid(), Name = "Garage" });

        var location = await _sut.CreateAsync(new LocationInput("garage"), _userId);

        Assert.Equal("garage", location.Name);
    }

    [Fact]
    public async Task UpdateAsync_NotFound_Throws()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _sut.UpdateAsync(Guid.NewGuid(), new LocationInput("Garage"), _userId));

        Assert.Equal(ErrorCodes.LocationNotFound, ex.Code);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesFieldsSavesAndLogs()
    {
        var location = new Location { Id = Guid.NewGuid(), Name = "Garage", Address = "Old", Latitude = 1, Longitude = 2 };
        _locations.GetByIdAsync(location.Id, Arg.Any<CancellationToken>()).Returns(location);

        await _sut.UpdateAsync(location.Id, new LocationInput("Basement", "New St 2", Notes: "Bring a cable"), _userId);

        Assert.Equal("Basement", location.Name);
        Assert.Equal("New St 2", location.Address);
        Assert.Null(location.Latitude);
        Assert.Null(location.Longitude);
        Assert.Equal("Bring a cable", location.Notes);
        await _locations.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditLog.Received(1).AddAsync(
            Arg.Is<AdminAuditLogEntry>(e => e.Action == AuditActions.LocationUpdated && e.Details!.Contains("Basement")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_KeepingOwnName_IsAllowed()
    {
        var location = new Location { Id = Guid.NewGuid(), Name = "Garage" };
        _locations.GetByIdAsync(location.Id, Arg.Any<CancellationToken>()).Returns(location);
        _locations.GetByNameAsync("Garage", Arg.Any<CancellationToken>()).Returns(location);

        await _sut.UpdateAsync(location.Id, new LocationInput("Garage", "Somewhere"), _userId);

        Assert.Equal("Somewhere", location.Address);
    }

    [Fact]
    public async Task UpdateAsync_NameOfOtherLocation_Throws()
    {
        var location = new Location { Id = Guid.NewGuid(), Name = "Garage" };
        _locations.GetByIdAsync(location.Id, Arg.Any<CancellationToken>()).Returns(location);
        _locations.GetByNameAsync("Basement", Arg.Any<CancellationToken>())
            .Returns(new Location { Id = Guid.NewGuid(), Name = "Basement" });

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _sut.UpdateAsync(location.Id, new LocationInput("Basement"), _userId));

        Assert.Equal(ErrorCodes.LocationNameTaken, ex.Code);
        Assert.Equal("Garage", location.Name);
    }

    [Fact]
    public async Task DeleteAsync_NotFound_Throws()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.DeleteAsync(Guid.NewGuid(), _userId));

        Assert.Equal(ErrorCodes.LocationNotFound, ex.Code);
    }

    [Fact]
    public async Task DeleteAsync_ReferencedByParties_ThrowsWithPartyCount()
    {
        var location = new Location { Id = Guid.NewGuid(), Name = "Garage" };
        _locations.GetByIdAsync(location.Id, Arg.Any<CancellationToken>()).Returns(location);
        _locations.CountPartiesAsync(location.Id, Arg.Any<CancellationToken>()).Returns(3);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.DeleteAsync(location.Id, _userId));

        Assert.Equal(ErrorCodes.LocationInUse, ex.Code);
        Assert.Equal([3], ex.Args);
        _locations.DidNotReceive().Remove(Arg.Any<Location>());
    }

    [Fact]
    public async Task DeleteAsync_Unused_RemovesSavesAndLogs()
    {
        var location = new Location { Id = Guid.NewGuid(), Name = "Garage" };
        _locations.GetByIdAsync(location.Id, Arg.Any<CancellationToken>()).Returns(location);

        await _sut.DeleteAsync(location.Id, _userId);

        _locations.Received(1).Remove(location);
        await _locations.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditLog.Received(1).AddAsync(
            Arg.Is<AdminAuditLogEntry>(e => e.Action == AuditActions.LocationDeleted && e.Details!.Contains("Garage")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WithoutPermission_Throws()
    {
        _permissions.EnsureAsync(_userId, Permissions.PartyManage, Arg.Any<CancellationToken>())
            .ThrowsAsync(new PermissionDeniedException(Permissions.PartyManage));

        await Assert.ThrowsAsync<PermissionDeniedException>(() => _sut.DeleteAsync(Guid.NewGuid(), _userId));

        _locations.DidNotReceive().Remove(Arg.Any<Location>());
    }
}
