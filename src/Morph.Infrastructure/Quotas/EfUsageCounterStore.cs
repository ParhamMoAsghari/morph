using Microsoft.EntityFrameworkCore;
using Morph.Infrastructure.Persistence;
using Morph.Infrastructure.Persistence.Quotas;
using Morph.Messaging.Ports;

namespace Morph.Infrastructure.Quotas;

public sealed class EfUsageCounterStore : IUsageCounterStore
{
    private readonly PlatformDbContext _db;

    public EfUsageCounterStore(PlatformDbContext db)
    {
        _db = db;
    }

    public async Task<long> IncrementAsync(
        Guid projectId,
        string metric,
        long delta = 1,
        DateOnly? day = null,
        CancellationToken cancellationToken = default)
    {
        var d = day ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var row = await _db.UsageCounters
            .FirstOrDefaultAsync(
                c => c.ProjectId == projectId && c.Metric == metric && c.Day == d,
                cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            row = new UsageCounterRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Metric = metric,
                Day = d,
                Value = delta
            };
            _db.UsageCounters.Add(row);
        }
        else
        {
            row.Value += delta;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return row.Value;
    }

    public async Task<long> GetAsync(
        Guid projectId,
        string metric,
        DateOnly? day = null,
        CancellationToken cancellationToken = default)
    {
        var d = day ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var value = await _db.UsageCounters.AsNoTracking()
            .Where(c => c.ProjectId == projectId && c.Metric == metric && c.Day == d)
            .Select(c => (long?)c.Value)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return value ?? 0;
    }
}
