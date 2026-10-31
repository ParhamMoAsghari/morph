using System.Text.Json;
using Morph.BuildingBlocks.Time;
using Morph.Messaging.Domain;
using Morph.Messaging.Ports;

namespace Morph.Messaging.Outbox;

public interface IOutboxPublisher
{
    Task PublishAsync<T>(
        string type,
        T payload,
        string? correlationId = null,
        DateTimeOffset? availableAt = null,
        CancellationToken cancellationToken = default);
}

public sealed class OutboxPublisher : IOutboxPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IOutboxStore _store;
    private readonly IClock _clock;

    public OutboxPublisher(IOutboxStore store, IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    public Task PublishAsync<T>(
        string type,
        T payload,
        string? correlationId = null,
        DateTimeOffset? availableAt = null,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = type,
            PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
            Status = OutboxStatus.Pending,
            CreatedAt = now,
            AvailableAt = availableAt ?? now,
            CorrelationId = correlationId
        };

        return _store.EnqueueAsync(message, cancellationToken);
    }
}
