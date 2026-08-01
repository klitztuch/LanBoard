using LanBoard.Application.Interfaces;
using LanBoard.Core.Entities;
using LanBoard.Infrastructure.Services;
using NSubstitute;

namespace LanBoard.Infrastructure.Tests.Services;

public class PartyMembershipServiceTests
{
    private readonly ILanPartyRepository _parties = Substitute.For<ILanPartyRepository>();
    private readonly IPartyMembershipRepository _memberships = Substitute.For<IPartyMembershipRepository>();
    private readonly PartyMembershipService _sut;

    public PartyMembershipServiceTests()
    {
        _sut = new PartyMembershipService(_parties, _memberships);
    }

    [Fact]
    public async Task IsMemberAsync_DelegatesToRepository()
    {
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        _memberships.ExistsAsync(userId, partyId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.IsMemberAsync(userId, partyId);

        Assert.True(result);
    }

    [Fact]
    public async Task JoinByInviteCodeAsync_InvalidCode_Throws()
    {
        _parties.FindByInviteCodeAsync("BADCODE", Arg.Any<CancellationToken>()).Returns((LanParty?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.JoinByInviteCodeAsync(Guid.NewGuid(), "BADCODE"));
        await _memberships.DidNotReceive().AddAsync(Arg.Any<PartyMembership>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinByInviteCodeAsync_ValidCode_CreatesMembershipAndSaves()
    {
        var userId = Guid.NewGuid();
        var party = new LanParty { Id = Guid.NewGuid(), Name = "Party", Location = "Loc", CreatedByUserId = Guid.NewGuid(), InviteCode = "ABC123" };
        _parties.FindByInviteCodeAsync("ABC123", Arg.Any<CancellationToken>()).Returns(party);
        _memberships.ExistsAsync(userId, party.Id, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.JoinByInviteCodeAsync(userId, "ABC123");

        Assert.Same(party, result);
        await _memberships.Received(1).AddAsync(
            Arg.Is<PartyMembership>(m => m.UserId == userId && m.PartyId == party.Id),
            Arg.Any<CancellationToken>());
        await _memberships.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinByInviteCodeAsync_CodeWithWhitespace_IsTrimmedBeforeLookup()
    {
        var party = new LanParty { Id = Guid.NewGuid(), Name = "Party", Location = "Loc", CreatedByUserId = Guid.NewGuid(), InviteCode = "ABC123" };
        _parties.FindByInviteCodeAsync("ABC123", Arg.Any<CancellationToken>()).Returns(party);

        await _sut.JoinByInviteCodeAsync(Guid.NewGuid(), "  ABC123  ");

        await _parties.Received(1).FindByInviteCodeAsync("ABC123", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinByInviteCodeAsync_AlreadyMember_IsIdempotent()
    {
        var userId = Guid.NewGuid();
        var party = new LanParty { Id = Guid.NewGuid(), Name = "Party", Location = "Loc", CreatedByUserId = Guid.NewGuid(), InviteCode = "ABC123" };
        _parties.FindByInviteCodeAsync("ABC123", Arg.Any<CancellationToken>()).Returns(party);
        _memberships.ExistsAsync(userId, party.Id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.JoinByInviteCodeAsync(userId, "ABC123");

        Assert.Same(party, result);
        await _memberships.DidNotReceive().AddAsync(Arg.Any<PartyMembership>(), Arg.Any<CancellationToken>());
        await _memberships.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
