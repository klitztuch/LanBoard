using LanBoard.Core.Entities;

namespace LanBoard.Infrastructure.Services;

internal static class SessionPlaytime
{
    // Each Session row spans a single continuous stretch of one game (a new row is
    // written on game change instead of overwriting the previous one, see
    // SessionService.SyncActiveSessionsAsync), so LastSeen - JoinedAt approximates
    // that stretch's playtime.
    public static TimeSpan Sum(IEnumerable<Session> sessions)
        => sessions.Aggregate(TimeSpan.Zero, (sum, s) => sum + (s.LastSeen - s.JoinedAt));
}
