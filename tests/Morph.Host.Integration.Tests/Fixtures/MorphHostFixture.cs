using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Morph.Infrastructure.Hosting;
using Morph.Plugins.Abstractions;
using Testcontainers.PostgreSql;
using Xunit;

namespace Morph.Host.Integration.Tests.Fixtures;

/// <summary>
/// Shared Testcontainers Postgres + WebApplicationFactory for host HTTP tests.
/// </summary>
public sealed class MorphHostFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("morph")
        .WithUsername("morph")
        .WithPassword("morph")
        .Build();

    private WebApplicationFactory<Program>? _factory;

    public string ConnectionString { get; private set; } = string.Empty;

    public IServiceProvider Services =>
        _factory?.Services ?? throw new InvalidOperationException("Fixture not initialized.");

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        ConnectionString = _postgres.GetConnectionString();

        Environment.SetEnvironmentVariable(DatabaseInitializer.SkipEnvironmentVariable, null);

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(WebHostDefaults.EnvironmentKey, "Development");
            builder.UseSetting("Morph:ConnectionStrings:Platform", ConnectionString);
            builder.UseSetting("Morph:Redis:ConnectionString", string.Empty);
            builder.UseSetting("Morph:ObjectStorage:Endpoint", string.Empty);
            builder.UseSetting("Morph:ObjectStorage:BucketPrefix", "morph");
            builder.UseSetting("Morph:Mail:SmtpHost", "127.0.0.1");
            builder.UseSetting("Morph:Mail:SmtpPort", "1");
            builder.UseSetting("Morph:Auth:PlatformClientSecret", "morph-platform-dev-secret");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISmsSender>();
                services.AddSingleton<ISmsSender, TestSmsSender>();
            });
        });
    }

    public HttpClient CreateClient()
    {
        if (_factory is null)
        {
            throw new InvalidOperationException("Fixture not initialized.");
        }

        return _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition("morph-host")]
public sealed class MorphHostCollection : ICollectionFixture<MorphHostFixture>;
