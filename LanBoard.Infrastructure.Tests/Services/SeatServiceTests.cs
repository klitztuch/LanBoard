using LanBoard.Application.Interfaces;
using LanBoard.Application.Notifications;
using LanBoard.Core.Entities;
using LanBoard.Infrastructure.Services;
using NSubstitute;

namespace LanBoard.Infrastructure.Tests.Services;

public class SeatServiceTests
{
    private static readonly Guid PartyId = Guid.NewGuid();

    private readonly ILanPartyRepository _parties = Substitute.For<ILanPartyRepository>();
    private readonly ISeatRepository _seats = Substitute.For<ISeatRepository>();
    private readonly ILanBoardNotifier _notifier = Substitute.For<ILanBoardNotifier>();

    private readonly SeatService _sut;

    public SeatServiceTests()
    {
        _sut = new SeatService(_parties, _seats, _notifier);
    }

    private static Seat CreateSeat(Guid? assignedUserId = null)
        => new() { Id = Guid.NewGuid(), PartyId = PartyId, Label = "A1", AssignedUserId = assignedUserId };

    [Fact]
    public async Task GetActivePartyWithSeatsAsync_NoActiveParty_ReturnsNull()
    {
        _parties.GetActiveAsync(Arg.Any<CancellationToken>()).Returns((LanParty?)null);

        var result = await _sut.GetActivePartyWithSeatsAsync();

        Assert.Null(result);
        await _parties.DidNotReceive().GetWithSeatsAndSessionsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetActivePartyWithSeatsAsync_ActiveParty_ReturnsPartyWithSeats()
    {
        var active = new LanParty { Id = PartyId, Name = "Party", Location = "Loc", CreatedByUserId = Guid.NewGuid() };
        var withSeats = new LanParty { Id = PartyId, Name = "Party", Location = "Loc", CreatedByUserId = Guid.NewGuid() };
        _parties.GetActiveAsync(Arg.Any<CancellationToken>()).Returns(active);
        _parties.GetWithSeatsAndSessionsAsync(PartyId, Arg.Any<CancellationToken>()).Returns(withSeats);

        var result = await _sut.GetActivePartyWithSeatsAsync();

        Assert.Same(withSeats, result);
    }

    [Fact]
    public async Task ClaimSeatAsync_SeatNotFound_Throws()
    {
        _seats.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Seat?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ClaimSeatAsync(Guid.NewGuid(), Guid.NewGuid()));
        _notifier.DidNotReceive().NotifyChanged();
    }

    [Fact]
    public async Task ClaimSeatAsync_SeatTakenByOtherUser_Throws()
    {
        var seat = CreateSeat(assignedUserId: Guid.NewGuid());
        _seats.GetByIdAsync(seat.Id, Arg.Any<CancellationToken>()).Returns(seat);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ClaimSeatAsync(seat.Id, Guid.NewGuid()));
        await _seats.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _notifier.DidNotReceive().NotifyChanged();
    }

    [Fact]
    public async Task ClaimSeatAsync_FreeSeat_AssignsAndNotifies()
    {
        var userId = Guid.NewGuid();
        var seat = CreateSeat();
        _seats.GetByIdAsync(seat.Id, Arg.Any<CancellationToken>()).Returns(seat);
        _seats.GetByUserAsync(PartyId, userId, Arg.Any<CancellationToken>()).Returns((Seat?)null);

        var result = await _sut.ClaimSeatAsync(seat.Id, userId);

        Assert.Equal(userId, result.AssignedUserId);
        await _seats.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _notifier.Received(1).NotifyChanged();
    }

    [Fact]
    public async Task ClaimSeatAsync_UserHasAnotherSeat_ReleasesOldSeatAndAssignsNew()
    {
        var userId = Guid.NewGuid();
        var newSeat = CreateSeat();
        var oldSeat = CreateSeat(assignedUserId: userId);
        _seats.GetByIdAsync(newSeat.Id, Arg.Any<CancellationToken>()).Returns(newSeat);
        _seats.GetByUserAsync(PartyId, userId, Arg.Any<CancellationToken>()).Returns(oldSeat);

        await _sut.ClaimSeatAsync(newSeat.Id, userId);

        Assert.Null(oldSeat.AssignedUserId);
        Assert.Equal(userId, newSeat.AssignedUserId);
    }

    [Fact]
    public async Task ReleaseSeatAsync_SeatNotFound_Throws()
    {
        _seats.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Seat?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ReleaseSeatAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task ReleaseSeatAsync_NotAssignedToUser_Throws()
    {
        var seat = CreateSeat(assignedUserId: Guid.NewGuid());
        _seats.GetByIdAsync(seat.Id, Arg.Any<CancellationToken>()).Returns(seat);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ReleaseSeatAsync(seat.Id, Guid.NewGuid()));
        _notifier.DidNotReceive().NotifyChanged();
    }

    [Fact]
    public async Task ReleaseSeatAsync_Assigned_ReleasesAndNotifies()
    {
        var userId = Guid.NewGuid();
        var seat = CreateSeat(assignedUserId: userId);
        _seats.GetByIdAsync(seat.Id, Arg.Any<CancellationToken>()).Returns(seat);

        await _sut.ReleaseSeatAsync(seat.Id, userId);

        Assert.Null(seat.AssignedUserId);
        await _seats.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _notifier.Received(1).NotifyChanged();
    }
}
