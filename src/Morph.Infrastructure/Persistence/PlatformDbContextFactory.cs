using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Morph.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF migrations (dotnet ef).
/// </summary>
public sealed class PlatformDbContextFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
    public PlatformDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("MORPH_ConnectionStrings__Platform")
            ?? "Host=localhost;Port=5432;Database=morph;Username=morph;Password=morph";

        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "platform");
            })
            .Options;

        return new PlatformDbContext(options);
    }
}
