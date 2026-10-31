namespace Morph.Messaging.Ports;

public interface IUsageCounterStore
{
    Task<long> IncrementAsync(
        Guid projectId,
        string metric,
        long delta = 1,
        DateOnly? day = null,
        CancellationToken cancellationToken = default);

    Task<long> GetAsync(
        Guid projectId,
        string metric,
        DateOnly? day = null,
        CancellationToken cancellationToken = default);
}

public static class UsageMetrics
{
    public const string ApiRequests = "api.requests";
    public const string WebhookDeliveries = "webhook.deliveries";
    public const string RealtimeSubscribes = "realtime.subscribes";
}
