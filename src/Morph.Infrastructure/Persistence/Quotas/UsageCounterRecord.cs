namespace Morph.Infrastructure.Persistence.Quotas;

public sealed class UsageCounterRecord
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public required string Metric { get; set; }

    public DateOnly Day { get; set; }

    public long Value { get; set; }
}
