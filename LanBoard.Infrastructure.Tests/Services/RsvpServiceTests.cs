using LanBoard.Application.Interfaces;
using LanBoard.Core.Entities;
using LanBoard.Infrastructure.Services;
using NSubstitute;

namespace LanBoard.Infrastructure.Tests.Services;

public class RsvpServiceTests
{
    private static readonly Guid PartyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly IRsvpRepository _rsvps = Substitute.For<IRsvpRepository>();
    private readonly RsvpService _sut;

    public RsvpServiceTests()
    {
        _sut = new RsvpService(_rsvps);
    }

    [Fact]
    public async Task GetMyRsvpAsync_DelegatesToRepository()
    {
        var rsvp = new Rsvp { Id = Guid.NewGuid(), UserId = UserId, PartyId = PartyId, IsAttending = true };
        _rsvps.GetByUserAndPartyAsync(UserId, PartyId, Arg.Any<CancellationToken>()).Returns(rsvp);

        var result = await _sut.GetMyRsvpAsync(UserId, PartyId);

        Assert.Same(rsvp, result);
    }

    [Fact]
    public async Task SetRsvpAsync_NoExistingRsvp_CreatesAndSaves()
    {
        _rsvps.GetByUserAndPartyAsync(UserId, PartyId, Arg.Any<CancellationToken>()).Returns((Rsvp?)null);

        var result = await _sut.SetRsvpAsync(UserId, PartyId, true);

        Assert.Equal(UserId, result.UserId);
        Assert.Equal(PartyId, result.PartyId);
        Assert.True(result.IsAttending);
        await _rsvps.Received(1).AddAsync(Arg.Any<Rsvp>(), Arg.Any<CancellationToken>());
        await _rsvps.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetRsvpAsync_ExistingRsvp_UpdatesInPlaceWithoutAdding()
    {
        var existing = new Rsvp { Id = Guid.NewGuid(), UserId = UserId, PartyId = PartyId, IsAttending = true, RespondedAt = DateTime.UtcNow.AddDays(-1) };
        _rsvps.GetByUserAndPartyAsync(UserId, PartyId, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await _sut.SetRsvpAsync(UserId, PartyId, false);

        Assert.Same(existing, result);
        Assert.False(existing.IsAttending);
        await _rsvps.DidNotReceive().AddAsync(Arg.Any<Rsvp>(), Arg.Any<CancellationToken>());
        await _rsvps.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRsvpsByPartyAsync_ReturnsDictionaryKeyedByUserId()
    {
        var rsvp = new Rsvp { Id = Guid.NewGuid(), UserId = UserId, PartyId = PartyId, IsAttending = true };
        _rsvps.GetByPartyAsync(PartyId, Arg.Any<CancellationToken>()).Returns([rsvp]);

        var result = await _sut.GetRsvpsByPartyAsync(PartyId);

        Assert.Same(rsvp, result[UserId]);
    }
}
