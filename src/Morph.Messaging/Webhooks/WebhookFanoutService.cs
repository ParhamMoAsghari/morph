using System.Text.Json;
using Morph.BuildingBlocks.Time;
using Morph.Messaging.Domain;
using Morph.Messaging.Outbox;
using Morph.Messaging.Ports;

namespace Morph.Messaging.Webhooks;

/// <summary>
/// Enqueues signed webhook deliveries for matching subscriptions after entity mutations.
/// </summary>
public sealed class WebhookFanoutService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IWebhookSubscriptionStore _subscriptions;
    private readonly IOutboxPublisher _outbox;
    private readonly IClock _clock;

    public WebhookFanoutService(
        IWebhookSubscriptionStore subscriptions,
        IOutboxPublisher outbox,
        IClock clock)
    {
        _subscriptions = subscriptions;
        _outbox = outbox;
        _clock = clock;
    }

    public async Task EnqueueEntityEventAsync(
        Guid projectId,
        Guid environmentId,
        string entitySlug,
        string eventType,
        Guid? recordId,
        object body,
        CancellationToken cancellationToken = default)
    {
        var matches = await _subscriptions
            .ListMatchingAsync(projectId, environmentId, entitySlug, eventType, cancellationToken)
            .ConfigureAwait(false);

        if (matches.Count == 0)
        {
            return;
        }

        var bodyJson = JsonSerializer.Serialize(body, JsonOptions);

        foreach (var sub in matches)
        {
            await _outbox.PublishAsync(
                    OutboxMessageTypes.WebhookDeliver,
                    new WebhookDeliveryPayload
                    {
                        SubscriptionId = sub.Id,
                        ProjectId = projectId,
                        EnvironmentId = environmentId,
                        TargetUrl = sub.TargetUrl,
                        SigningSecret = sub.SigningSecret,
                        EventType = eventType,
                        EntitySlug = entitySlug,
                        RecordId = recordId,
                        BodyJson = bodyJson
                    },
                    correlationId: recordId?.ToString("N"),
                    availableAt: _clock.UtcNow,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
