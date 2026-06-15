using LanBoard.Application.Interfaces;
using LanBoard.Application.Seats;
using LanBoard.Application.Users;
using LanBoard.Infrastructure.ApiClients;
using LanBoard.Infrastructure.ApiClients.Steam;
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
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<ISeatService, SeatService>();

        builder.Services.AddOptions<SteamConfiguration>()
            .BindConfiguration("Steam")
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
