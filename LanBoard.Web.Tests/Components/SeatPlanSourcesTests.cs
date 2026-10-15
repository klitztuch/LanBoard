using LanBoard.Core.Entities;
using LanBoard.Web.Components;

namespace LanBoard.Web.Tests.Components;

public class SeatPlanSourcesTests
{
    private static readonly Guid Garage = Guid.NewGuid();
    private static readonly Guid Basement = Guid.NewGuid();

    [Fact]
    public void Candidates_AreOtherPartiesWithSeats_NewestFirst()
    {
        var target = Party(Garage, 2026, seats: 0);
        var old = Party(Basement, 2024);
        var recent = Party(Garage, 2025);
        var empty = Party(Garage, 2025, seats: 0);

        var candidates = SeatPlanSources.Candidates(target, [old, target, empty, recent]);

        Assert.Equal([recent, old], candidates);
    }

    [Fact]
    public void Default_IsMostRecentCandidateAtSameLocation()
    {
        var target = Party(Garage, 2026, seats: 0);
        var otherLocation = Party(Basement, 2025);
        var newestHere = Party(Garage, 2024);
        var olderHere = Party(Garage, 2023);

        var source = SeatPlanSources.Default(target, SeatPlanSources.Candidates(target, [olderHere, otherLocation, newestHere]));

        Assert.Same(newestHere, source);
    }

    [Fact]
    public void Default_NoCandidateAtSameLocation_IsNull()
    {
        var target = Party(Garage, 2026, seats: 0);

        Assert.Null(SeatPlanSources.Default(target, [Party(Basement, 2025)]));
    }

    private static LanParty Party(Guid locationId, int year, int seats = 2) => new()
    {
        Id = Guid.NewGuid(),
        Name = $"LAN {year}",
        Date = new DateTime(year, 1, 1),
        LocationId = locationId,
        Seats = [.. Enumerable.Range(0, seats).Select(i => new Seat { Label = $"{i}" })]
    };
}
