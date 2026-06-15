using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Platform.Infrastructure.Database;

/// <summary>
/// Design-time factory so <c>dotnet ef migrations …</c> can build the context without running
/// the API host. Migrations are authored against <b>PostgreSQL</b> (the production provider), so
/// the factory uses Npgsql by default. EF doesn't connect for <c>migrations add/script</c>, so the
/// placeholder connection string is fine; override it with EF__DESIGN__CONNECTION if you ever run
/// <c>database update</c> at design time.
/// </summary>
internal sealed class PlatformDbContextFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
    public PlatformDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("EF__DESIGN__CONNECTION")
            ?? "Host=localhost;Port=5432;Database=spawnweaver;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseNpgsql(connection)
            .Options;

        return new PlatformDbContext(options);
    }
}
