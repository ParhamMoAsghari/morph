using Morph.Messaging.Domain;
using Morph.Messaging.Outbox;
using Morph.Messaging.Ports;
using Morph.Messaging.Webhooks;

namespace Morph.Runtime.Api.Realtime;

/// <summary>
/// Fans entity mutations to realtime outbox + webhook subscriptions.
/// </summary>
public sealed class EntityEventPublisher
{
    private readonly IOutboxPublisher _outbox;
    private readonly WebhookFanoutService _webhooks;

    public EntityEventPublisher(IOutboxPublisher outbox, WebhookFanoutService webhooks)
    {
        _outbox = outbox;
        _webhooks = webhooks;
    }

    public async Task PublishAsync(
        Guid projectId,
        Guid environmentId,
        string entitySlug,
        string eventType,
        Guid? recordId,
        object body,
        CancellationToken cancellationToken = default)
    {
        await _outbox.PublishAsync(
                OutboxMessageTypes.RealtimeEntity,
                new RealtimeEntityPayload
                {
                    EnvironmentId = environmentId,
                    EntitySlug = entitySlug,
                    EventType = eventType,
                    RecordId = recordId,
                    Data = body
                },
                correlationId: recordId?.ToString("N"),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        await _webhooks
            .EnqueueEntityEventAsync(
                projectId,
                environmentId,
                entitySlug,
                eventType,
                recordId,
                body,
                cancellationToken)
            .ConfigureAwait(false);
    }
}