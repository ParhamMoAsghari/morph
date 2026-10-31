namespace Morph.Tenancy.Quotas;

public interface IQuotaService
{
    ProjectQuotaLimits Limits { get; }

    Task<QuotaCheckResult> CheckApiRequestAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<QuotaCheckResult> CheckWebhookSubscriptionAsync(Guid projectId, CancellationToken cancellationToken = default);
}

public sealed record QuotaCheckResult(bool Allowed, string? Reason = null, long? Current = null, long? Limit = null)
{
    public static QuotaCheckResult Ok() => new(true);

    public static QuotaCheckResult SoftExceeded(string reason, long current, long limit) =>
        new(false, reason, current, limit);
}
