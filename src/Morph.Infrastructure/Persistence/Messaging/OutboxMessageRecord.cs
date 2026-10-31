namespace Morph.Infrastructure.Persistence.Messaging;

public sealed class OutboxMessageRecord
{
    public Guid Id { get; set; }

    public required string Type { get; set; }

    public required string PayloadJson { get; set; }

    public required string Status { get; set; }

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset AvailableAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public string? CorrelationId { get; set; }
}
