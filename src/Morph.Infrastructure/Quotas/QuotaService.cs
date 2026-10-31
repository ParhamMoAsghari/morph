using Microsoft.Extensions.Options;
using Morph.Messaging.Ports;
using Morph.Tenancy.Quotas;

namespace Morph.Infrastructure.Quotas;

public sealed class QuotaService : IQuotaService
{
    private readonly IUsageCounterStore _usage;
    private readonly IWebhookSubscriptionStore _webhooks;
    private readonly ProjectQuotaLimits _limits;

    public QuotaService(
        IUsageCounterStore usage,
        IWebhookSubscriptionStore webhooks,
        IOptions<ProjectQuotaLimits> limits)
    {
        _usage = usage;
        _webhooks = webhooks;
        _limits = limits.Value;
    }

    public ProjectQuotaLimits Limits => _limits;

    public async Task<QuotaCheckResult> CheckApiRequestAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var current = await _usage
            .GetAsync(projectId, UsageMetrics.ApiRequests, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (current >= _limits.MaxApiRequestsPerDay)
        {
            return QuotaCheckResult.SoftExceeded(
                $"Daily API request soft quota exceeded ({current}/{_limits.MaxApiRequestsPerDay}).",
                current,
                _limits.MaxApiRequestsPerDay);
        }

        return QuotaCheckResult.Ok();
    }

    public async Task<QuotaCheckResult> CheckWebhookSubscriptionAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var current = await _webhooks.CountByProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (current >= _limits.MaxWebhookSubscriptionsPerProject)
        {
            return QuotaCheckResult.SoftExceeded(
                $"Webhook subscription soft quota exceeded ({current}/{_limits.MaxWebhookSubscriptionsPerProject}).",
                current,
                _limits.MaxWebhookSubscriptionsPerProject);
        }

        return QuotaCheckResult.Ok();
    }
}
