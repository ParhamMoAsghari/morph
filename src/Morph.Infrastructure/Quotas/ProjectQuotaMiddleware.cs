using Microsoft.AspNetCore.Http;
using Morph.Messaging.Ports;
using Morph.Tenancy.Quotas;

namespace Morph.Infrastructure.Quotas;

/// <summary>
/// Soft quota middleware: increments daily API request counters when <c>X-Morph-Project-Id</c> is present.
/// Returns 429 when soft limit exceeded (MVP soft enforcement).
/// </summary>
public sealed class ProjectQuotaMiddleware
{
    public const string ProjectIdHeader = "X-Morph-Project-Id";

    private readonly RequestDelegate _next;

    public ProjectQuotaMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IQuotaService quotas,
        IUsageCounterStore usage)
    {
        if (context.Request.Headers.TryGetValue(ProjectIdHeader, out var raw)
            && Guid.TryParse(raw.ToString(), out var projectId)
            && projectId != Guid.Empty)
        {
            var check = await quotas.CheckApiRequestAsync(projectId, context.RequestAborted).ConfigureAwait(false);
            if (!check.Allowed)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(new
                {
                    type = "https://morph.local/problems/quota-exceeded",
                    title = "Soft quota exceeded",
                    detail = check.Reason,
                    current = check.Current,
                    limit = check.Limit
                }).ConfigureAwait(false);
                return;
            }

            await usage
                .IncrementAsync(projectId, UsageMetrics.ApiRequests, cancellationToken: context.RequestAborted)
                .ConfigureAwait(false);
        }

        await _next(context).ConfigureAwait(false);
    }
}
