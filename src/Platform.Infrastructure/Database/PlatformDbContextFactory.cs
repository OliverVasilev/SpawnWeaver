using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Platform.Infrastructure.Database;

/// <summary>
/// Design-time factory so <c>dotnet ef migrations …</c> can build the context
/// without running the API host.
/// </summary>
internal sealed class PlatformDbContextFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
    public PlatformDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseSqlite("Data Source=spawnweaver-design.db")
            .Options;

        return new PlatformDbContext(options);
    }
}
