namespace LanBoard.Web.Services;

/// <summary>
/// Runs each call on a fresh DI scope, so every operation gets its own <c>DbContext</c>.
/// Components inject this instead of the service: a circuit-scoped context only supports one operation at a time.
/// </summary>
public sealed class PerCall<TService>(IServiceScopeFactory scopeFactory) where TService : notnull
{
    public async Task<TResult> Run<TResult>(Func<TService, Task<TResult>> call)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await call(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public async Task Run(Func<TService, Task> call)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await call(scope.ServiceProvider.GetRequiredService<TService>());
    }
}
