using LanBoard.Application.Authorization;
using LanBoard.Application.Errors;
using LanBoard.Application.Interfaces;
using LanBoard.Core.Entities;
using LanBoard.Infrastructure.Services;
using NSubstitute;

namespace LanBoard.Infrastructure.Tests.Services;

public class AdminServiceTests
{
    private readonly ILanPartyRepository _parties = Substitute.For<ILanPartyRepository>();
    private readonly ISeatRepository _seats = Substitute.For<ISeatRepository>();
    private readonly IAdminAuditLogRepository _auditLog = Substitute.For<IAdminAuditLogRepository>();
    private readonly IPermissionService _permissions = Substitute.For<IPermissionService>();
    private readonly AdminService _sut;

    public AdminServiceTests()
    {
        _sut = new AdminService(_parties, _seats, _auditLog, _permissions);
    }

    [Fact]
    public async Task CreatePartyAsync_GeneratesInviteCodeAndPersists()
    {
        var createdByUserId = Guid.NewGuid();

        var result = await _sut.CreatePartyAsync("LAN #1", new DateTime(2026, 8, 1), "Garage", createdByUserId);

        Assert.Equal("LAN #1", result.Name);
        Assert.Equal("Garage", result.Location);
        Assert.Equal(createdByUserId, result.CreatedByUserId);
        Assert.False(string.IsNullOrWhiteSpace(result.InviteCode));
        await _parties.Received(1).AddAsync(result, Arg.Any<CancellationToken>());
        await _parties.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePartyAsync_LogsAuditEntry()
    {
        var createdByUserId = Guid.NewGuid();

        await _sut.CreatePartyAsync("LAN #1", new DateTime(2026, 8, 1), "Garage", createdByUserId);

        await _auditLog.Received(1).AddAsync(
            Arg.Is<AdminAuditLogEntry>(e => e.UserId == createdByUserId && e.Action == "PartyCreated" && e.Details!.Contains("LAN #1")),
            Arg.Any<CancellationToken>());
        await _auditLog.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdatePartyAsync_PartyNotFound_Throws()
    {
        _parties.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((LanParty?)null);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _sut.UpdatePartyAsync(Guid.NewGuid(), "Name", DateTime.Today, "Loc", Guid.NewGuid()));
        Assert.Equal(ErrorCodes.PartyNotFound, ex.Code);
    }

    [Fact]
    public async Task UpdatePartyAsync_UpdatesFieldsAndSaves()
    {
        var party = new LanParty { Id = Guid.NewGuid(), Name = "Old", Location = "Old Loc", CreatedByUserId = Guid.NewGuid() };
        _parties.GetByIdAsync(party.Id, Arg.Any<CancellationToken>()).Returns(party);
        var newDate = new DateTime(2026, 9, 1);

        var performedByUserId = Guid.NewGuid();
        await _sut.UpdatePartyAsync(party.Id, "New", newDate, "New Loc", performedByUserId);

        Assert.Equal("New", party.Name);
        Assert.Equal(newDate, party.Date);
        Assert.Equal("New Loc", party.Location);
        await _parties.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditLog.Received(1).AddAsync(
            Arg.Is<AdminAuditLogEntry>(e => e.UserId == performedByUserId && e.Action == "PartyUpdated"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeletePartyAsync_PartyNotFound_Throws()
    {
        _parties.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((LanParty?)null);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.DeletePartyAsync(Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal(ErrorCodes.PartyNotFound, ex.Code);
    }

    [Fact]
    public async Task DeletePartyAsync_RemovesAndSaves()
    {
        var party = new LanParty { Id = Guid.NewGuid(), Name = "Party", Location = "Loc", CreatedByUserId = Guid.NewGuid() };
        _parties.GetByIdAsync(party.Id, Arg.Any<CancellationToken>()).Returns(party);

        var performedByUserId = Guid.NewGuid();
        await _sut.DeletePartyAsync(party.Id, performedByUserId);

        _parties.Received(1).Remove(party);
        await _parties.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditLog.Received(1).AddAsync(
            Arg.Is<AdminAuditLogEntry>(e => e.UserId == performedByUserId && e.Action == "PartyDeleted" && e.Details!.Contains("Party")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddSeatAsync_CreatesSeatWithLabelFromCoordinates()
    {
        var partyId = Guid.NewGuid();

        var seat = await _sut.AddSeatAsync(partyId, 3, 5, Guid.NewGuid());

        Assert.Equal(partyId, seat.PartyId);
        Assert.Equal(3, seat.X);
        Assert.Equal(5, seat.Y);
        Assert.Equal("35", seat.Label);
        await _seats.Received(1).AddAsync(seat, Arg.Any<CancellationToken>());
        await _seats.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveSeatAsync_SeatNotFound_Throws()
    {
        _seats.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Seat?)null);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.RemoveSeatAsync(Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal(ErrorCodes.SeatNotFound, ex.Code);
    }

    [Fact]
    public async Task RemoveSeatAsync_SeatAssigned_Throws()
    {
        var seat = new Seat { Id = Guid.NewGuid(), Label = "A1", AssignedUserId = Guid.NewGuid() };
        _seats.GetByIdAsync(seat.Id, Arg.Any<CancellationToken>()).Returns(seat);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.RemoveSeatAsync(seat.Id, Guid.NewGuid()));
        Assert.Equal(ErrorCodes.SeatAssigned, ex.Code);
        _seats.DidNotReceive().Remove(Arg.Any<Seat>());
    }

    [Fact]
    public async Task RemoveSeatAsync_FreeSeat_RemovesAndSaves()
    {
        var seat = new Seat { Id = Guid.NewGuid(), Label = "A1" };
        _seats.GetByIdAsync(seat.Id, Arg.Any<CancellationToken>()).Returns(seat);

        await _sut.RemoveSeatAsync(seat.Id, Guid.NewGuid());

        _seats.Received(1).Remove(seat);
        await _seats.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetActivePartyAsync_PreviouslyActiveParty_DeactivatesItAndActivatesTarget()
    {
        var previouslyActive = new LanParty { Id = Guid.NewGuid(), Name = "Old", Location = "Loc", CreatedByUserId = Guid.NewGuid(), IsActive = true };
        var target = new LanParty { Id = Guid.NewGuid(), Name = "New", Location = "Loc", CreatedByUserId = Guid.NewGuid() };
        _parties.GetActiveAsync(Arg.Any<CancellationToken>()).Returns(previouslyActive);
        _parties.GetByIdAsync(target.Id, Arg.Any<CancellationToken>()).Returns(target);

        var performedByUserId = Guid.NewGuid();
        await _sut.SetActivePartyAsync(target.Id, performedByUserId);

        Assert.False(previouslyActive.IsActive);
        Assert.True(target.IsActive);
        await _parties.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditLog.Received(1).AddAsync(
            Arg.Is<AdminAuditLogEntry>(e => e.UserId == performedByUserId && e.Action == "PartyActivated" && e.Details!.Contains("New")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetActivePartyAsync_NoPreviouslyActiveParty_ActivatesTarget()
    {
        var target = new LanParty { Id = Guid.NewGuid(), Name = "New", Location = "Loc", CreatedByUserId = Guid.NewGuid() };
        _parties.GetActiveAsync(Arg.Any<CancellationToken>()).Returns((LanParty?)null);
        _parties.GetByIdAsync(target.Id, Arg.Any<CancellationToken>()).Returns(target);

        await _sut.SetActivePartyAsync(target.Id, Guid.NewGuid());

        Assert.True(target.IsActive);
        await _parties.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetActivePartyAsync_TargetNotFound_Throws()
    {
        _parties.GetActiveAsync(Arg.Any<CancellationToken>()).Returns((LanParty?)null);
        _parties.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((LanParty?)null);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.SetActivePartyAsync(Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal(ErrorCodes.PartyNotFound, ex.Code);
    }

    [Fact]
    public async Task GetRecentAuditLogAsync_DelegatesToRepository()
    {
        var entry = new AdminAuditLogEntry { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Action = "PartyCreated", CreatedAt = DateTime.UtcNow };
        _auditLog.GetRecentAsync(10, Arg.Any<CancellationToken>()).Returns([entry]);

        var result = await _sut.GetRecentAuditLogAsync(Guid.NewGuid(), 10);

        Assert.Same(entry, Assert.Single(result));
    }

    public static TheoryData<string> PartyManageMethods => ["CreateParty", "UpdateParty", "DeleteParty", "SetActiveParty", "AddSeat", "RemoveSeat"];

    [Theory]
    [MemberData(nameof(PartyManageMethods))]
    public async Task PartyMutation_CallerLacksPartyManage_ThrowsAndChangesNothing(string method)
    {
        var deniedUserId = Guid.NewGuid();
        _permissions.EnsureAsync(deniedUserId, Permissions.PartyManage, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new PermissionDeniedException(Permissions.PartyManage)));

        Func<Task> act = method switch
        {
            "CreateParty" => () => _sut.CreatePartyAsync("LAN", DateTime.Today, "Loc", deniedUserId),
            "UpdateParty" => () => _sut.UpdatePartyAsync(Guid.NewGuid(), "LAN", DateTime.Today, "Loc", deniedUserId),
            "DeleteParty" => () => _sut.DeletePartyAsync(Guid.NewGuid(), deniedUserId),
            "SetActiveParty" => () => _sut.SetActivePartyAsync(Guid.NewGuid(), deniedUserId),
            "AddSeat" => () => _sut.AddSeatAsync(Guid.NewGuid(), 1, 1, deniedUserId),
            "RemoveSeat" => () => _sut.RemoveSeatAsync(Guid.NewGuid(), deniedUserId),
            _ => throw new ArgumentOutOfRangeException(nameof(method))
        };

        await Assert.ThrowsAsync<PermissionDeniedException>(act);

        await _parties.DidNotReceive().AddAsync(Arg.Any<LanParty>(), Arg.Any<CancellationToken>());
        _parties.DidNotReceive().Remove(Arg.Any<LanParty>());
        await _parties.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _seats.DidNotReceive().AddAsync(Arg.Any<Seat>(), Arg.Any<CancellationToken>());
        _seats.DidNotReceive().Remove(Arg.Any<Seat>());
        await _seats.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditLog.DidNotReceive().AddAsync(Arg.Any<AdminAuditLogEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRecentAuditLogAsync_CallerLacksAuditView_Throws()
    {
        var deniedUserId = Guid.NewGuid();
        _permissions.EnsureAsync(deniedUserId, Permissions.AuditView, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new PermissionDeniedException(Permissions.AuditView)));

        await Assert.ThrowsAsync<PermissionDeniedException>(() => _sut.GetRecentAuditLogAsync(deniedUserId));

        await _auditLog.DidNotReceive().GetRecentAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
