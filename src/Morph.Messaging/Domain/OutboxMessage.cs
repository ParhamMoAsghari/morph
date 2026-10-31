namespace Morph.Messaging.Domain;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    public required string Type { get; set; }

    public required string PayloadJson { get; set; }

    public OutboxStatus Status { get; set; } = OutboxStatus.Pending;

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset AvailableAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public string? CorrelationId { get; set; }
}

public enum OutboxStatus
{
    Pending = 0,
    Processing = 1,
    Processed = 2,
    Dead = 3
}

public static class OutboxMessageTypes
{
    public const string SendEmail = "mail.send";

    public const string WebhookDeliver = "webhook.deliver";

    public const string RealtimeEntity = "realtime.entity";
}
