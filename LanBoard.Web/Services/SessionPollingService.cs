using LanBoard.Application.Seats;
using LanBoard.Application.Sessions;
using LanBoard.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace LanBoard.Web.Services;

public class SessionPollingService(
    IServiceScopeFactory scopeFactory,
    IOptions<SessionTrackingConfiguration> options,
    ILogger<SessionPollingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds));

        do
        {
            try
            {
                await PollAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Session polling tick failed.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PollAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var seatService = scope.ServiceProvider.GetRequiredService<ISeatService>();
        var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();

        var party = await seatService.GetActivePartyWithSeatsAsync(ct: ct);
        if (party is null || party.Seats.All(s => s.AssignedUserId is null))
            return;

        await sessionService.SyncActiveSessionsAsync(party.Id, ct);
    }
}
