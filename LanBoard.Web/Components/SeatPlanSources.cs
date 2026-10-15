using LanBoard.Core.Entities;

namespace LanBoard.Web.Components;

/// <summary>Which parties a seat plan can be copied from (parties need their seats and location id loaded).</summary>
public static class SeatPlanSources
{
    /// <summary>Other parties that have seats, newest first.</summary>
    public static IReadOnlyList<LanParty> Candidates(LanParty target, IEnumerable<LanParty> parties)
        => parties
            .Where(p => p.Id != target.Id && p.Seats.Count > 0)
            .OrderByDescending(p => p.Date)
            .ToList();

    /// <summary>The most recent candidate at the target's location, if any.</summary>
    public static LanParty? Default(LanParty target, IEnumerable<LanParty> candidates)
        => candidates
            .Where(p => p.LocationId == target.LocationId)
            .MaxBy(p => p.Date);
}
