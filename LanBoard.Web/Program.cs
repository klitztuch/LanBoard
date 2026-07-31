using LanBoard.Application.Notifications;
using LanBoard.Infrastructure;
using LanBoard.ServiceDefaults;
using LanBoard.Web.Components;
using LanBoard.Web.Extensions;
using LanBoard.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddInfrastructure();
builder.AddAuth();

builder.Services.AddSingleton<ILanBoardNotifier, LanBoardNotifier>();
builder.Services.AddHostedService<SessionPollingService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromSeconds(30));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
    app.MapDevAuthEndpoints();

app.MapGet("/auth/login", (HttpContext ctx, string? returnUrl) =>
    ctx.ChallengeAsync("Steam", new AuthenticationProperties { RedirectUri = returnUrl ?? "/" }));

app.MapGet("/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

await app.MigrateDatabaseAsync();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
