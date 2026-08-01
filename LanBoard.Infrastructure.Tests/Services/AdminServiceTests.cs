using LanBoard.Application.Interfaces;
using LanBoard.Core.Entities;
using LanBoard.Infrastructure.Services;
using NSubstitute;

namespace LanBoard.Infrastructure.Tests.Services;

public class AdminServiceTests
{
    private readonly ILanPartyRepository _parties = Substitute.For<ILanPartyRepository>();
    private readonly ISeatRepository _seats = Substitute.For<ISeatRepository>();
    private readonly AdminService _sut;

    public AdminServiceTests()
    {
        _sut = new AdminService(_parties, _seats);
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
    public async Task UpdatePartyAsync_PartyNotFound_Throws()
    {
        _parties.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((LanParty?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdatePartyAsync(Guid.NewGuid(), "Name", DateTime.Today, "Loc"));
    }

    [Fact]
    public async Task UpdatePartyAsync_UpdatesFieldsAndSaves()
    {
        var party = new LanParty { Id = Guid.NewGuid(), Name = "Old", Location = "Old Loc", CreatedByUserId = Guid.NewGuid() };
        _parties.GetByIdAsync(party.Id, Arg.Any<CancellationToken>()).Returns(party);
        var newDate = new DateTime(2026, 9, 1);

        await _sut.UpdatePartyAsync(party.Id, "New", newDate, "New Loc");

        Assert.Equal("New", party.Name);
        Assert.Equal(newDate, party.Date);
        Assert.Equal("New Loc", party.Location);
        await _parties.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateInfoBoardAsync_PartyNotFound_Throws()
    {
        _parties.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((LanParty?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateInfoBoardAsync(Guid.NewGuid(), "WLAN: lan2026"));
    }

    [Fact]
    public async Task UpdateInfoBoardAsync_SetsFieldAndSaves()
    {
        var party = new LanParty { Id = Guid.NewGuid(), Name = "Party", Location = "Loc", CreatedByUserId = Guid.NewGuid() };
        _parties.GetByIdAsync(party.Id, Arg.Any<CancellationToken>()).Returns(party);

        await _sut.UpdateInfoBoardAsync(party.Id, "WLAN: lan2026");

        Assert.Equal("WLAN: lan2026", party.InfoBoard);
        await _parties.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeletePartyAsync_PartyNotFound_Throws()
    {
        _parties.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((LanParty?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.DeletePartyAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeletePartyAsync_RemovesAndSaves()
    {
        var party = new LanParty { Id = Guid.NewGuid(), Name = "Party", Location = "Loc", CreatedByUserId = Guid.NewGuid() };
        _parties.GetByIdAsync(party.Id, Arg.Any<CancellationToken>()).Returns(party);

        await _sut.DeletePartyAsync(party.Id);

        _parties.Received(1).Remove(party);
        await _parties.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddSeatAsync_CreatesSeatWithLabelFromCoordinates()
    {
        var partyId = Guid.NewGuid();

        var seat = await _sut.AddSeatAsync(partyId, 3, 5);

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

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.RemoveSeatAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task RemoveSeatAsync_SeatAssigned_Throws()
    {
        var seat = new Seat { Id = Guid.NewGuid(), Label = "A1", AssignedUserId = Guid.NewGuid() };
        _seats.GetByIdAsync(seat.Id, Arg.Any<CancellationToken>()).Returns(seat);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.RemoveSeatAsync(seat.Id));
        _seats.DidNotReceive().Remove(Arg.Any<Seat>());
    }

    [Fact]
    public async Task RemoveSeatAsync_FreeSeat_RemovesAndSaves()
    {
        var seat = new Seat { Id = Guid.NewGuid(), Label = "A1" };
        _seats.GetByIdAsync(seat.Id, Arg.Any<CancellationToken>()).Returns(seat);

        await _sut.RemoveSeatAsync(seat.Id);

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

        await _sut.SetActivePartyAsync(target.Id);

        Assert.False(previouslyActive.IsActive);
        Assert.True(target.IsActive);
        await _parties.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetActivePartyAsync_NoPreviouslyActiveParty_ActivatesTarget()
    {
        var target = new LanParty { Id = Guid.NewGuid(), Name = "New", Location = "Loc", CreatedByUserId = Guid.NewGuid() };
        _parties.GetActiveAsync(Arg.Any<CancellationToken>()).Returns((LanParty?)null);
        _parties.GetByIdAsync(target.Id, Arg.Any<CancellationToken>()).Returns(target);

        await _sut.SetActivePartyAsync(target.Id);

        Assert.True(target.IsActive);
        await _parties.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetActivePartyAsync_TargetNotFound_Throws()
    {
        _parties.GetActiveAsync(Arg.Any<CancellationToken>()).Returns((LanParty?)null);
        _parties.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((LanParty?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.SetActivePartyAsync(Guid.NewGuid()));
    }
}
