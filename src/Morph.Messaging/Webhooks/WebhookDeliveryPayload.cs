namespace Morph.Messaging.Webhooks;

public sealed class WebhookDeliveryPayload
{
    public Guid SubscriptionId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid EnvironmentId { get; set; }

    public required string TargetUrl { get; set; }

    public required string SigningSecret { get; set; }

    public required string EventType { get; set; }

    public required string EntitySlug { get; set; }

    public Guid? RecordId { get; set; }

    public required string BodyJson { get; set; }
}
