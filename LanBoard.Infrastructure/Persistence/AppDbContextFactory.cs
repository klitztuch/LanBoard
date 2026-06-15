using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LanBoard.Infrastructure.Persistence;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
#pragma warning disable S2068 // Design-time factory for EF migrations, not a production credential
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=lanboard;Username=postgres;Password=postgres")
            .Options;
#pragma warning restore S2068
        return new AppDbContext(options);
    }
}
