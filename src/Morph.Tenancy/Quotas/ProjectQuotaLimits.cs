namespace Morph.Tenancy.Quotas;

/// <summary>
/// Soft project quotas (MVP). Enforcement warns / returns 429 when exceeded; billing later.
/// </summary>
public sealed class ProjectQuotaLimits
{
    public const string SectionName = "Morph:Quotas";

    public int MaxEnvironmentsPerProject { get; set; } = 10;

    public int MaxDefinitionsPerEnvironment { get; set; } = 200;

    public int MaxApiRequestsPerDay { get; set; } = 100_000;

    public int MaxWebhookSubscriptionsPerProject { get; set; } = 50;

    public long MaxStorageBytesPerProject { get; set; } = 5L * 1024 * 1024 * 1024;
}
