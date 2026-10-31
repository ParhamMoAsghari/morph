using Morph.Messaging.Domain;

namespace Morph.Messaging.Ports;

public interface IOutboxStore
{
    Task EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OutboxMessage>> DequeuePendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        Guid id,
        string error,
        DateTimeOffset nextAvailableAt,
        bool dead,
        CancellationToken cancellationToken = default);

    Task<int> DeleteProcessedOlderThanAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default);
}
