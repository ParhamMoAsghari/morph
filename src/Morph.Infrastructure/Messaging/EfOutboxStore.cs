using Microsoft.EntityFrameworkCore;
using Morph.Infrastructure.Persistence;
using Morph.Infrastructure.Persistence.Messaging;
using Morph.Messaging.Domain;
using Morph.Messaging.Ports;

namespace Morph.Infrastructure.Messaging;

public sealed class EfOutboxStore : IOutboxStore
{
    private readonly PlatformDbContext _db;

    public EfOutboxStore(PlatformDbContext db)
    {
        _db = db;
    }

    public async Task EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        _db.OutboxMessages.Add(ToRecord(message));
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OutboxMessage>> DequeuePendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var rows = await _db.OutboxMessages
            .Where(m => m.Status == nameof(OutboxStatus.Pending) && m.AvailableAt <= now)
            .OrderBy(m => m.AvailableAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count == 0)
        {
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return [];
        }

        foreach (var row in rows)
        {
            row.Status = nameof(OutboxStatus.Processing);
            row.AttemptCount += 1;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(ToDomain).ToList();
    }

    public async Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _db.OutboxMessages.FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return;
        }

        row.Status = nameof(OutboxStatus.Processed);
        row.ProcessedAt = DateTimeOffset.UtcNow;
        row.LastError = null;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(
        Guid id,
        string error,
        DateTimeOffset nextAvailableAt,
        bool dead,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.OutboxMessages.FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return;
        }

        row.Status = dead ? nameof(OutboxStatus.Dead) : nameof(OutboxStatus.Pending);
        row.LastError = error.Length > 2000 ? error[..2000] : error;
        row.AvailableAt = nextAvailableAt;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> DeleteProcessedOlderThanAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default)
    {
        return await _db.OutboxMessages
            .Where(m => m.Status == nameof(OutboxStatus.Processed) && m.ProcessedAt != null && m.ProcessedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static OutboxMessageRecord ToRecord(OutboxMessage message) => new()
    {
        Id = message.Id,
        Type = message.Type,
        PayloadJson = message.PayloadJson,
        Status = message.Status.ToString(),
        AttemptCount = message.AttemptCount,
        LastError = message.LastError,
        CreatedAt = message.CreatedAt,
        AvailableAt = message.AvailableAt,
        ProcessedAt = message.ProcessedAt,
        CorrelationId = message.CorrelationId
    };

    private static OutboxMessage ToDomain(OutboxMessageRecord row) => new()
    {
        Id = row.Id,
        Type = row.Type,
        PayloadJson = row.PayloadJson,
        Status = Enum.TryParse<OutboxStatus>(row.Status, true, out var status) ? status : OutboxStatus.Pending,
        AttemptCount = row.AttemptCount,
        LastError = row.LastError,
        CreatedAt = row.CreatedAt,
        AvailableAt = row.AvailableAt,
        ProcessedAt = row.ProcessedAt,
        CorrelationId = row.CorrelationId
    };
}
