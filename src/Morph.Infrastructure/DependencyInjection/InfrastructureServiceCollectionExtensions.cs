using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Morph.Access.DependencyInjection;
using Morph.BuildingBlocks.Time;
using Morph.DataPlane.Cache;
using Morph.DataPlane.DependencyInjection;
using Morph.Files.DependencyInjection;
using Morph.Files.Ports;
using Morph.Files.Storage;
using Morph.Identity.Auth;
using Morph.Identity.Users;
using Morph.Infrastructure.Audit;
using Morph.Infrastructure.Files;
using Morph.Infrastructure.Messaging;
using Morph.Infrastructure.Options;
using Morph.Infrastructure.Persistence;
using Morph.Infrastructure.Quotas;
using Morph.Infrastructure.Realtime;
using Morph.Infrastructure.Tenancy;
using Morph.Infrastructure.Webhooks;
using Morph.Crypto.DependencyInjection;
using Morph.Functions.Runtime.DependencyInjection;
using Morph.Infrastructure.Functions;
using Morph.Mail.DependencyInjection;
using Morph.Messaging.DependencyInjection;
using Morph.Messaging.Ports;
using Morph.ObjectStore.DependencyInjection;
using Morph.ProcessEngine.Ports;
using Morph.Sms.DependencyInjection;
using Morph.StorageEngine.DependencyInjection;
using Morph.Tenancy.Quotas;
using Npgsql;
using OpenIddict.Abstractions;
using StackExchange.Redis;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Morph.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddMorphInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MorphOptions>(configuration.GetSection(MorphOptions.SectionName));

        var morph = configuration.GetSection(MorphOptions.SectionName).Get<MorphOptions>() ?? new MorphOptions();
        var connectionString = ResolvePlatformConnectionString(configuration, morph);

        services.Configure<ProjectQuotaLimits>(options =>
        {
            options.MaxEnvironmentsPerProject = morph.Quotas.MaxEnvironmentsPerProject;
            options.MaxDefinitionsPerEnvironment = morph.Quotas.MaxDefinitionsPerEnvironment;
            options.MaxApiRequestsPerDay = morph.Quotas.MaxApiRequestsPerDay;
            options.MaxWebhookSubscriptionsPerProject = morph.Quotas.MaxWebhookSubscriptionsPerProject;
            options.MaxStorageBytesPerProject = morph.Quotas.MaxStorageBytesPerProject;
        });

        services.AddSingleton<IClock, SystemClock>();

        services.AddDbContext<PlatformDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "platform");
                npgsql.MigrationsAssembly(typeof(PlatformDbContext).Assembly.FullName);
            });
            options.UseOpenIddict();
        });

        services
            .AddDataProtection()
            .PersistKeysToDbContext<PlatformDbContext>()
            .SetApplicationName("Morph");

        services
            .AddIdentity<PlatformUser, IdentityRole<Guid>>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<PlatformDbContext>()
            .AddDefaultTokenProviders();

        var issuer = string.IsNullOrWhiteSpace(morph.Auth.Issuer)
            ? AuthConstants.Issuer
            : morph.Auth.Issuer;

        services
            .AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<PlatformDbContext>();
            })
            .AddServer(options =>
            {
                options
                    .SetTokenEndpointUris("connect/token")
                    .SetAuthorizationEndpointUris("connect/authorize")
                    .SetIntrospectionEndpointUris("connect/introspect")
                    .SetRevocationEndpointUris("connect/revoke");

                options
                    .AllowPasswordFlow()
                    .AllowRefreshTokenFlow()
                    .AllowClientCredentialsFlow()
                    .AllowAuthorizationCodeFlow()
                    .RequireProofKeyForCodeExchange();

                options.RegisterScopes(
                    Scopes.OpenId,
                    Scopes.Profile,
                    Scopes.Email,
                    Scopes.OfflineAccess,
                    AuthConstants.ScopePlatformApi);

                options
                    .AddDevelopmentEncryptionCertificate()
                    .AddDevelopmentSigningCertificate();

                options
                    .UseAspNetCore()
                    .EnableTokenEndpointPassthrough()
                    .EnableAuthorizationEndpointPassthrough()
                    .DisableTransportSecurityRequirement();

                options.SetAccessTokenLifetime(TimeSpan.FromHours(1));
                options.SetRefreshTokenLifetime(TimeSpan.FromDays(14));
                options.SetIssuer(new Uri(issuer));
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
                options.AddAudiences("morph-platform-api");
            });

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme =
                OpenIddict.Validation.AspNetCore.OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme =
                OpenIddict.Validation.AspNetCore.OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        });

        services.AddAuthorization();

        var redis = morph.Redis.ConnectionString
            ?? configuration["MORPH_Redis__ConnectionString"]
            ?? configuration.GetConnectionString("Redis");

        if (!string.IsNullOrWhiteSpace(redis))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redis;
                options.InstanceName = "morph:";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        AddMorphHealthChecks(services, connectionString, redis, morph);

        // Phase 2: EF definition persistence + Npgsql DDL (replaces in-memory EntityEngine ports).
        services.AddMorphEntityEnginePersistence();

        // Phase 8: Process Engine EF persistence (in-memory ports replaced; engine registered by Platform.Api).
        services.AddMorphProcessEnginePersistence();

        // Phase 9: Functions runtime + process invoke adapter (Platform.Api also calls AddMorphFunctionsRuntime).
        services.AddMorphFunctionsRuntime();
        services.RemoveAll<IProcessFunctionInvoker>();
        services.AddScoped<IProcessFunctionInvoker, ProcessFunctionInvokerAdapter>();

        // Phase 3: Access + DataPlane (Npgsql data source + env schema resolver).
        services.AddMorphAccess();
        services.AddSingleton(NpgsqlDataSource.Create(connectionString));
        services.AddScoped<IEnvironmentSchemaResolver, EfEnvironmentSchemaResolver>();
        services.AddMorphDataPlane();

        // Phase 4: Files + outbox + mail + Hangfire.
        services.TryAddScoped<IFileMetadataStore, EfFileMetadataStore>();
        services.TryAddScoped<IOutboxStore, EfOutboxStore>();

        if (!string.IsNullOrWhiteSpace(morph.ObjectStorage.Endpoint))
        {
            services.TryAddSingleton<IObjectStorage, S3ObjectStorage>();
        }
        else
        {
            services.TryAddSingleton<IObjectStorage, InMemoryObjectStorage>();
        }

        var physicalBucket = string.IsNullOrWhiteSpace(morph.ObjectStorage.BucketPrefix)
            ? "morph"
            : morph.ObjectStorage.BucketPrefix;
        services.AddMorphFiles(physicalBucket);
        services.AddMorphObjectStore();
        services.AddMorphMessaging();
        services.AddMorphMail(configuration);
        services.AddMorphHangfire(configuration, connectionString);
        services.AddMorphSms();

        // Phase 10: Crypto + ObjectStore + StorageEngine facades.
        services.AddMorphCrypto(configuration);
        services.AddMorphStorageEngine();

        // Phase 6: webhooks, audit, quotas, SignalR backplane.
        services.AddHttpClient(SsrfGuardedWebhookHttpSender.ClientName)
            .AddHttpMessageHandler(() => new WebhookSsrfHandler());
        services.TryAddScoped<IWebhookHttpSender, SsrfGuardedWebhookHttpSender>();
        services.TryAddScoped<IWebhookSubscriptionStore, EfWebhookSubscriptionStore>();
        services.TryAddScoped<IAuditStore, EfAuditStore>();
        services.TryAddScoped<IUsageCounterStore, EfUsageCounterStore>();
        services.TryAddScoped<IQuotaService, QuotaService>();

        AddMorphSignalR(services, redis);

        return services;
    }

    private static void AddMorphSignalR(IServiceCollection services, string? redis)
    {
        var builder = services.AddSignalR();
        if (!string.IsNullOrWhiteSpace(redis))
        {
            builder.AddStackExchangeRedis(redis, options =>
            {
                options.Configuration.ChannelPrefix = RedisChannel.Literal("morph-signalr");
            });
        }
    }


    private static void AddMorphHealthChecks(
        IServiceCollection services,
        string connectionString,
        string? redis,
        MorphOptions morph)
    {
        var builder = services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgres", tags: ["ready", "db"])
            .AddDbContextCheck<PlatformDbContext>("ef", tags: ["ready", "db"]);

        if (!string.IsNullOrWhiteSpace(redis))
        {
            builder.AddRedis(redis, name: "redis", tags: ["ready", "cache"]);
        }

        if (!string.IsNullOrWhiteSpace(morph.ObjectStorage.Endpoint)
            && Uri.TryCreate(morph.ObjectStorage.Endpoint, UriKind.Absolute, out var minioUri))
        {
            var healthUri = new Uri(minioUri, "/minio/health/live");
            builder.AddUrlGroup(healthUri, name: "minio", failureStatus: HealthStatus.Degraded, tags: ["ready", "storage"]);
        }
    }

    private static string ResolvePlatformConnectionString(IConfiguration configuration, MorphOptions morph)
    {
        var value =
            configuration["MORPH_ConnectionStrings__Platform"]
            ?? morph.ConnectionStrings.Platform
            ?? configuration.GetConnectionString("Platform");

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                "Platform connection string is not configured. Set Morph:ConnectionStrings:Platform or MORPH_ConnectionStrings__Platform.");
        }

        return value;
    }
}
