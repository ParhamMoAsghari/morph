namespace Morph.Messaging.Ports;

public interface IWebhookSubscriptionStore
{
    Task<WebhookSubscription> CreateAsync(WebhookSubscription subscription, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WebhookSubscription>> ListByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WebhookSubscription>> ListMatchingAsync(
        Guid projectId,
        Guid environmentId,
        string entitySlug,
        string eventType,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid projectId, Guid subscriptionId, CancellationToken cancellationToken = default);

    Task<int> CountByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}

public sealed class WebhookSubscription
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Guid? EnvironmentId { get; set; }

    public required string TargetUrl { get; set; }

    public required string SigningSecret { get; set; }

    /// <summary>Entity slug filter; null/empty = all entities in scope.</summary>
    public string? EntitySlug { get; set; }

    /// <summary>Event types filter (e.g. record.created); empty = all.</summary>
    public IReadOnlyList<string> EventTypes { get; set; } = [];

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}
