using LanBoard.Application.Admin;
using LanBoard.Application.Interfaces;
using LanBoard.Application.Rsvps;
using LanBoard.Application.Seats;
using LanBoard.Application.Sessions;
using LanBoard.Application.Users;
using LanBoard.Infrastructure.ApiClients;
using LanBoard.Infrastructure.ApiClients.Steam;
using LanBoard.Infrastructure.Configuration;
using LanBoard.Infrastructure.Persistence;
using LanBoard.Infrastructure.Persistence.Repositories;
using LanBoard.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LanBoard.Infrastructure;

public static class InfrastructureExtensions
{
    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<AppDbContext>("postgres");

        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<ILanPartyRepository, LanPartyRepository>();
        builder.Services.AddScoped<ISeatRepository, SeatRepository>();
        builder.Services.AddScoped<ISessionRepository, SessionRepository>();
        builder.Services.AddScoped<IRsvpRepository, RsvpRepository>();
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<ISeatService, SeatService>();
        builder.Services.AddScoped<IAdminService, AdminService>();
        builder.Services.AddScoped<ISessionService, SessionService>();
        builder.Services.AddScoped<IRsvpService, RsvpService>();

        builder.Services.AddOptions<SteamConfiguration>()
            .BindConfiguration("Steam")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddOptions<SessionTrackingConfiguration>()
            .BindConfiguration("SessionTracking")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddHttpClient<ISteamApiClient, SteamApiClient>((sp, client) =>
        {
            var config = sp.GetRequiredService<IOptions<SteamConfiguration>>().Value;
            client.BaseAddress = new Uri(config.BaseUrl);
        });

        return builder;
    }

    public static async Task MigrateDatabaseAsync(this IHost host)
    {
        if (!host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
            return;

        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }
}
