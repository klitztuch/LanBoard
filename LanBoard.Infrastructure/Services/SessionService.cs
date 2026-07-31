using LanBoard.Application.Interfaces;
using LanBoard.Application.Notifications;
using LanBoard.Application.Sessions;
using LanBoard.Core.Entities;
using LanBoard.Infrastructure.ApiClients.Steam;

namespace LanBoard.Infrastructure.Services;

public class SessionService(
    ISessionRepository sessions,
    ISeatRepository seats,
    IUserRepository users,
    ISteamApiClient steamApiClient,
    ILanBoardNotifier notifier) : ISessionService
{
    public async Task SyncActiveSessionsAsync(Guid partyId, CancellationToken ct = default)
    {
        var seatedUserIds = (await seats.GetByPartyAsync(partyId, ct))
            .Where(s => s.AssignedUserId is not null)
            .Select(s => s.AssignedUserId!.Value)
            .Distinct()
            .ToHashSet();

        if (seatedUserIds.Count == 0)
            return;

        var steamIdToUserId = (await users.GetAllWithIdentitiesAsync(ct))
            .Where(u => seatedUserIds.Contains(u.Id))
            .SelectMany(u => u.Identities
                .Where(i => i.Provider == "Steam")
                .Select(i => (SteamId: i.ProviderUserId, UserId: u.Id)))
            .ToDictionary(x => x.SteamId, x => x.UserId);

        if (steamIdToUserId.Count == 0)
            return;

        var summaries = await steamApiClient.GetPlayerSummariesAsync(steamIdToUserId.Keys, ct);

        var activeByUser = (await sessions.GetActiveByPartyAsync(partyId, ct))
            .GroupBy(s => s.UserId)
            .ToDictionary(g => g.Key, g => g.First());

        var now = DateTime.UtcNow;
        var dataChanged = false;
        var visibleChanged = false;

        foreach (var summary in summaries)
        {
            if (string.IsNullOrEmpty(summary.GameId))
                continue;

            if (!steamIdToUserId.TryGetValue(summary.SteamId, out var userId))
                continue;

            if (activeByUser.TryGetValue(userId, out var active) && active.GameAppId == summary.GameId)
            {
                active.LastSeen = now;
                dataChanged = true;
            }
            else
            {
                // Intentionally adds a new row instead of updating the previous one when the
                // game changes, so past sessions remain in the table as a history log.
                await sessions.AddAsync(new Session
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    PartyId = partyId,
                    GameAppId = summary.GameId,
                    GameName = summary.GameExtraInfo,
                    JoinedAt = now,
                    LastSeen = now
                }, ct);

                dataChanged = true;
                visibleChanged = true;
            }
        }

        if (dataChanged)
            await sessions.SaveChangesAsync(ct);

        if (visibleChanged)
            notifier.NotifyChanged();
    }

    public async Task<IReadOnlyDictionary<Guid, Session>> GetActiveSessionsByPartyAsync(Guid partyId, CancellationToken ct = default)
        => (await sessions.GetActiveByPartyAsync(partyId, ct))
            .GroupBy(s => s.UserId)
            .ToDictionary(g => g.Key, g => g.First());
}
