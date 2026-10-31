using Microsoft.Extensions.Logging;
using Morph.Messaging.Ports;

namespace Morph.Infrastructure.Jobs;

public sealed class OutboxCleanupJob
{
    public const string JobId = "morph-outbox-cleanup";

    private readonly IOutboxStore _store;
    private readonly ILogger<OutboxCleanupJob> _logger;

    public OutboxCleanupJob(IOutboxStore store, ILogger<OutboxCleanupJob> logger)
    {
        _store = store;
        _logger = logger;
    }

    public Task Execute() => ExecuteCoreAsync(CancellationToken.None);

    private async Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-14);
        var deleted = await _store.DeleteProcessedOlderThanAsync(cutoff, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Outbox cleanup removed {Count} processed messages older than {Cutoff}.", deleted, cutoff);
    }
}
