namespace Morph.Infrastructure.Persistence.Webhooks;

public sealed class WebhookSubscriptionRecord
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Guid? EnvironmentId { get; set; }

    public required string TargetUrl { get; set; }

    public required string SigningSecret { get; set; }

    public string? EntitySlug { get; set; }

    public string EventTypesJson { get; set; } = "[]";

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}
