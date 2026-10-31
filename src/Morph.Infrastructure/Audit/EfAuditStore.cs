using Morph.BuildingBlocks.Time;
using Morph.Infrastructure.Persistence;
using Morph.Infrastructure.Persistence.Audit;
using Morph.Messaging.Ports;

namespace Morph.Infrastructure.Audit;

public sealed class EfAuditStore : IAuditStore
{
    private readonly PlatformDbContext _db;
    private readonly IClock _clock;

    public EfAuditStore(PlatformDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task AppendAsync(AuditEventEntry entry, CancellationToken cancellationToken = default)
    {
        _db.AuditEvents.Add(new AuditEventRecord
        {
            Id = entry.Id == Guid.Empty ? Guid.NewGuid() : entry.Id,
            Action = entry.Action,
            ActorPrincipalId = entry.ActorPrincipalId,
            OrganizationId = entry.OrganizationId,
            ProjectId = entry.ProjectId,
            EnvironmentId = entry.EnvironmentId,
            ResourceType = entry.ResourceType,
            ResourceId = entry.ResourceId,
            DetailJson = entry.DetailJson,
            OccurredAt = entry.OccurredAt == default ? _clock.UtcNow : entry.OccurredAt
        });

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
