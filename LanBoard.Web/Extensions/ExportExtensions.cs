using System.Text;
using LanBoard.Application.Seats;
using LanBoard.Application.Users;

namespace LanBoard.Web.Extensions;

public static class ExportExtensions
{
    public static IEndpointRouteBuilder MapExportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/export/attendees", async (Guid? partyId, IUserService userService, ISeatService seatService) =>
        {
            var users = await userService.GetAllWithIdentitiesAsync();
            var party = await seatService.GetActivePartyWithSeatsAsync(partyId);

            var seatByUser = new Dictionary<Guid, string>();
            foreach (var seat in party?.Seats ?? [])
            {
                if (seat.AssignedUserId is { } userId)
                    seatByUser[userId] = seat.Label;
            }

            var csv = new StringBuilder();
            csv.AppendLine("Name,Sitzplatz,Steam-Profil");
            foreach (var user in users)
            {
                var seatLabel = seatByUser.GetValueOrDefault(user.Id, "");
                var steamId = user.Identities.FirstOrDefault(i => i.Provider == "Steam")?.ProviderUserId;
                var steamUrl = steamId is null ? "" : $"https://steamcommunity.com/profiles/{steamId}";
                csv.AppendLine($"{CsvField(user.DisplayName)},{CsvField(seatLabel)},{CsvField(steamUrl)}");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return Results.File(bytes, "text/csv", $"teilnehmer-{DateTime.UtcNow:yyyyMMdd}.csv");
        }).RequireAuthorization("Admin");

        return app;
    }

    private static string CsvField(string value)
        => value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
