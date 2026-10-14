using LanBoard.Application.Interfaces;
using LanBoard.Core.Entities;
using LanBoard.Infrastructure.Services;
using NSubstitute;

namespace LanBoard.Infrastructure.Tests.Services;

public class PartyServiceTests
{
    private readonly ILanPartyRepository _parties = Substitute.For<ILanPartyRepository>();
    private readonly PartyService _sut;

    public PartyServiceTests()
    {
        _sut = new PartyService(_parties);
    }

    [Fact]
    public async Task GetAllAsync_NoParties_ReturnsEmptyList()
    {
        _parties.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);

        var result = await _sut.GetAllAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_MapsPartiesToSummariesInRepositoryOrder()
    {
        var active = new LanParty
        {
            Id = Guid.NewGuid(),
            Name = "Summer LAN",
            Date = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            Location = "Basement",
            InviteCode = "SECRET1",
            IsActive = true
        };
        var past = new LanParty
        {
            Id = Guid.NewGuid(),
            Name = "Winter LAN",
            Date = new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc),
            Location = "Garage",
            InviteCode = "SECRET2",
            IsActive = false
        };
        _parties.GetAllAsync(Arg.Any<CancellationToken>()).Returns([active, past]);

        var result = await _sut.GetAllAsync();

        Assert.Collection(result,
            s =>
            {
                Assert.Equal(active.Id, s.Id);
                Assert.Equal("Summer LAN", s.Name);
                Assert.Equal(active.Date, s.Date);
                Assert.Equal("Basement", s.Location);
                Assert.True(s.IsActive);
            },
            s =>
            {
                Assert.Equal(past.Id, s.Id);
                Assert.False(s.IsActive);
            });
    }
}
