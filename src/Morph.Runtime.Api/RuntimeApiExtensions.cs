using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Morph.Messaging.Ports;
using Morph.Runtime.Api.Data;
using Morph.Runtime.Api.Files;
using Morph.Runtime.Api.GraphQL;
using Morph.Runtime.Api.Realtime;
using Morph.Runtime.Api.Sql;

namespace Morph.Runtime.Api;

public static class RuntimeApiExtensions
{
    public static IServiceCollection AddMorphRuntimeApi(this IServiceCollection services)
    {
        services.TryAddScoped<RealtimeChannelAuthorizer>();
        services.TryAddScoped<EntityEventPublisher>();
        services.TryAddScoped<IRealtimeEntityBroadcaster, SignalRRealtimeEntityBroadcaster>();
        services.TryAddScoped<MorphDataGraphqlService>();
        services.AddHttpContextAccessor();

        services
            .AddGraphQLServer()
            .AddAuthorization()
            .AddQueryType<MorphGraphqlQueries>()
            .AddMutationType<MorphGraphqlMutations>()
            .ModifyRequestOptions(o => o.IncludeExceptionDetails = true);

        return services;
    }

    public static IEndpointRouteBuilder MapMorphRuntimeApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDataEndpoints();
        endpoints.MapFileEndpoints();
        endpoints.MapSqlEndpoints();
        endpoints.MapGraphQL("/graphql").RequireAuthorization();
        endpoints.MapHub<MorphRealtimeHub>(RealtimeChannels.HubPath);
        return endpoints;
    }
}
