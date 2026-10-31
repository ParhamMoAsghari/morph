using Microsoft.EntityFrameworkCore;
using Morph.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Morph.DataPlane.Integration.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("morph")
        .WithUsername("morph")
        .WithPassword("morph")
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseNpgsql(ConnectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "platform");
                npgsql.MigrationsAssembly(typeof(PlatformDbContext).Assembly.FullName);
            })
            .Options;

        return new PlatformDbContext(options);
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition("dataplane-postgres")]
public sealed class DataPlanePostgresCollection : ICollectionFixture<PostgresFixture>;
