using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Morph.EntityEngine.DependencyInjection;
using Morph.Platform.Api.Access;
using Morph.Platform.Api.Auth;
using Morph.Platform.Api.Backup;
using Morph.Platform.Api.Credentials;
using Morph.Platform.Api.Definitions;
using Morph.Platform.Api.Functions;
using Morph.Platform.Api.Migrations;
using Morph.Platform.Api.Processes;
using Morph.Platform.Api.Sms;
using Morph.Platform.Api.Tenancy;
using Morph.Platform.Api.Webhooks;
using Morph.ProcessEngine.DependencyInjection;

namespace Morph.Platform.Api;

public static class PlatformApiExtensions
{
    public static IServiceCollection AddMorphPlatformApi(this IServiceCollection services)
    {
        services.AddMorphEntityEngine();
        services.AddMorphProcessEngine();
        services.AddControllers();
        services.AddProblemDetails();
        return services;
    }

    public static IEndpointRouteBuilder MapMorphPlatformApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapControllers();
        endpoints.MapAuthEndpoints();
        endpoints.MapTenancyEndpoints();
        endpoints.MapDefinitionEndpoints();
        endpoints.MapMigrationEndpoints();
        endpoints.MapAccessEndpoints();
        endpoints.MapWebhookEndpoints();
        endpoints.MapCredentialEndpoints();
        endpoints.MapDefinitionBackupEndpoints();
        endpoints.MapProcessEndpoints();
        endpoints.MapFunctionEndpoints();
        endpoints.MapSmsEndpoints();
        return endpoints;
    }
}

