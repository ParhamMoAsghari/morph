using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Morph.Identity.Auth;
using Morph.Infrastructure.Options;
using Morph.Infrastructure.Persistence;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Morph.Infrastructure.Hosting;

public sealed class DatabaseInitializer : IHostedService
{
    public const string SkipEnvironmentVariable = "MORPH_SkipDatabaseInitialization";

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(IServiceProvider serviceProvider, ILogger<DatabaseInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.Equals(
                Environment.GetEnvironmentVariable(SkipEnvironmentVariable),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Skipping platform database initialization ({Variable}=true).", SkipEnvironmentVariable);
            return;
        }

        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        _logger.LogInformation("Applying platform EF migrations…");
        await db.Database.MigrateAsync(cancellationToken);

        await SeedOpenIddictApplicationsAsync(scope.ServiceProvider, cancellationToken);
        _logger.LogInformation("Platform database ready.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task SeedOpenIddictApplicationsAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var manager = services.GetRequiredService<IOpenIddictApplicationManager>();
        var auth = services.GetRequiredService<IOptions<MorphOptions>>().Value.Auth;

        if (await manager.FindByClientIdAsync(AuthConstants.PlatformClientId, cancellationToken) is not null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(auth.PlatformClientSecret))
        {
            throw new InvalidOperationException(
                "Morph:Auth:PlatformClientSecret is required to seed the OpenIddict platform client.");
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = AuthConstants.PlatformClientId,
            ClientSecret = auth.PlatformClientSecret,
            DisplayName = "Morph Platform (dev)",
            ClientType = ClientTypes.Confidential,
            ConsentType = ConsentTypes.Implicit,
            Permissions =
            {
                Permissions.Endpoints.Token,
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Revocation,
                Permissions.Endpoints.Introspection,
                Permissions.GrantTypes.Password,
                Permissions.GrantTypes.RefreshToken,
                Permissions.GrantTypes.ClientCredentials,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Email,
                Permissions.Scopes.Profile,
                Permissions.Scopes.Roles,
                Permissions.Prefixes.Scope + AuthConstants.ScopePlatformApi,
                Permissions.Prefixes.Scope + Scopes.OfflineAccess
            },
            Requirements =
            {
                Requirements.Features.ProofKeyForCodeExchange
            }
        };

        await manager.CreateAsync(descriptor, cancellationToken);
    }
}
