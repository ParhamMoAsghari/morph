using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Morph.Messaging.Ports;

namespace Morph.Messaging.Outbox;

public interface IOutboxDispatchService
{
    Task<int> DispatchBatchAsync(CancellationToken cancellationToken = default);
}

public sealed class OutboxDispatchService : IOutboxDispatchService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDispatchService> _logger;
    private readonly int _batchSize;

    public OutboxDispatchService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxDispatchService> logger,
        int batchSize = 32)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _batchSize = batchSize;
    }

    public async Task<int> DispatchBatchAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
        var handlers = scope.ServiceProvider
            .GetServices<IOutboxHandler>()
            .GroupBy(h => h.MessageType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var batch = await store.DequeuePendingAsync(_batchSize, cancellationToken).ConfigureAwait(false);
        var processed = 0;

        foreach (var message in batch)
        {
            if (!handlers.TryGetValue(message.Type, out var handler))
            {
                _logger.LogWarning("No outbox handler for type {Type}; marking dead.", message.Type);
                await store
                    .MarkFailedAsync(message.Id, $"No handler for '{message.Type}'.", DateTimeOffset.UtcNow, dead: true, cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            try
            {
                await handler.HandleAsync(message, cancellationToken).ConfigureAwait(false);
                await store.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
                processed++;
            }
            catch (Exception ex)
            {
                var attempts = message.AttemptCount;
                var dead = attempts >= 8;
                var delay = TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, Math.Min(attempts, 8))));
                _logger.LogError(ex, "Outbox message {Id} failed (attempt {Attempt}).", message.Id, attempts);
                await store
                    .MarkFailedAsync(
                        message.Id,
                        ex.Message,
                        DateTimeOffset.UtcNow.Add(delay),
                        dead,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return processed;
    }
}
