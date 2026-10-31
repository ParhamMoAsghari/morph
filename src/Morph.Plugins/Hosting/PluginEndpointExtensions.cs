using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Morph.Plugins.Abstractions;
using Morph.Plugins.Loading;
using Morph.Plugins.Webhooks;

namespace Morph.Plugins.Hosting;

public static class PluginEndpointExtensions
{
    public static IEndpointRouteBuilder MapMorphPlugins(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMethods(
            "/plugins/v1/{pluginId}/{**pathSuffix}",
            ["GET", "POST", "PUT", "PATCH", "DELETE"],
            async (string pluginId, string? pathSuffix, HttpContext context) =>
            {
                var suffix = string.IsNullOrWhiteSpace(pathSuffix) ? "webhook" : pathSuffix.Trim('/');
                var webhooks = context.RequestServices.GetRequiredService<IInboundWebhookRegistry>();
                if (webhooks.TryResolve(pluginId, suffix, out var webhook))
                {
                    await webhook.Handler.HandleAsync(context, context.RequestAborted).ConfigureAwait(false);
                    return;
                }

                var routes = context.RequestServices.GetRequiredService<IPluginHttpRouteRegistry>();
                if (routes.TryResolve(pluginId, suffix, out var route))
                {
                    await route.Handler(context, context.RequestAborted).ConfigureAwait(false);
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = $"No plugin route for '{pluginId}/{suffix}'."
                }).ConfigureAwait(false);
            });

        endpoints.MapGet("/plugins/v1", (PluginLoader pluginLoader, IProcessActivityRegistry activities) =>
        {
            var payload = pluginLoader.Loaded.Select(p => new
            {
                id = p.Manifest.Id,
                version = p.Manifest.Version,
                displayName = p.Manifest.DisplayName,
                capabilities = p.Manifest.Capabilities,
                permissions = p.Manifest.Permissions
            });

            return Results.Ok(new
            {
                plugins = payload,
                activities = activities.GetAll().Select(a => new { a.PluginId, a.TypeName })
            });
        });

        return endpoints;
    }
}
