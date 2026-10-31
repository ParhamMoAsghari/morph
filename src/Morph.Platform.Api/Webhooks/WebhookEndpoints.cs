using System.Security.Cryptography;
using Morph.BuildingBlocks.Security;
using Morph.BuildingBlocks.Time;
using Morph.Messaging.Ports;
using Morph.Tenancy.Quotas;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Morph.Platform.Api.Webhooks;

public static class WebhookEndpoints
{
    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/projects/{projectId:guid}/webhooks")
            .WithTags("Webhooks")
            .RequireAuthorization();

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapDelete("/{subscriptionId:guid}", DeleteAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid projectId,
        IWebhookSubscriptionStore store,
        CancellationToken cancellationToken)
    {
        var items = await store.ListByProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        return Results.Ok(items.Select(ToDto));
    }

    private static async Task<IResult> CreateAsync(
        Guid projectId,
        CreateWebhookRequest request,
        IWebhookSubscriptionStore store,
        IQuotaService quotas,
        IAuditStore audit,
        IClock clock,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TargetUrl)
            || !Uri.TryCreate(request.TargetUrl, UriKind.Absolute, out var uri))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["targetUrl"] = ["A valid absolute HTTP(S) URL is required."]
            });
        }

        try
        {
            await SsrfGuard.EnsurePublicEndpointAsync(uri, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["targetUrl"] = [ex.Message]
            });
        }

        var quota = await quotas.CheckWebhookSubscriptionAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (!quota.Allowed)
        {
            return Results.Json(
                new { detail = quota.Reason, current = quota.Current, limit = quota.Limit },
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        var secret = string.IsNullOrWhiteSpace(request.SigningSecret)
            ? Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()
            : request.SigningSecret.Trim();

        var created = await store.CreateAsync(
                new WebhookSubscription
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    EnvironmentId = request.EnvironmentId,
                    TargetUrl = uri.ToString(),
                    SigningSecret = secret,
                    EntitySlug = string.IsNullOrWhiteSpace(request.EntitySlug)
                        ? null
                        : request.EntitySlug.Trim().ToLowerInvariant(),
                    EventTypes = request.EventTypes ?? [],
                    IsEnabled = true,
                    CreatedAt = clock.UtcNow
                },
                cancellationToken)
            .ConfigureAwait(false);

        await audit.AppendAsync(
                new AuditEventEntry
                {
                    Action = AuditActions.WebhookCreate,
                    ActorPrincipalId = http.User.FindFirst("sub")?.Value,
                    ProjectId = projectId,
                    EnvironmentId = request.EnvironmentId,
                    ResourceType = "webhook",
                    ResourceId = created.Id.ToString("D"),
                    DetailJson = $"{{\"url\":\"{created.TargetUrl}\"}}",
                    OccurredAt = clock.UtcNow
                },
                cancellationToken)
            .ConfigureAwait(false);

        return Results.Created(
            $"/v1/projects/{projectId}/webhooks/{created.Id}",
            new
            {
                created.Id,
                created.ProjectId,
                created.EnvironmentId,
                created.TargetUrl,
                signingSecret = secret,
                created.EntitySlug,
                created.EventTypes,
                created.IsEnabled,
                created.CreatedAt
            });
    }

    private static async Task<IResult> DeleteAsync(
        Guid projectId,
        Guid subscriptionId,
        IWebhookSubscriptionStore store,
        CancellationToken cancellationToken)
    {
        var deleted = await store.DeleteAsync(projectId, subscriptionId, cancellationToken).ConfigureAwait(false);
        return deleted ? Results.NoContent() : Results.NotFound(new { detail = "Webhook subscription not found." });
    }

    private static object ToDto(WebhookSubscription s) => new
    {
        s.Id,
        s.ProjectId,
        s.EnvironmentId,
        s.TargetUrl,
        s.EntitySlug,
        s.EventTypes,
        s.IsEnabled,
        s.CreatedAt
    };
}

public sealed record CreateWebhookRequest(
    string TargetUrl,
    Guid? EnvironmentId = null,
    string? EntitySlug = null,
    IReadOnlyList<string>? EventTypes = null,
    string? SigningSecret = null);
