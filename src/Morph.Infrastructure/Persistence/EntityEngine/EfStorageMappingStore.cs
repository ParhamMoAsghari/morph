using Microsoft.EntityFrameworkCore;
using Morph.EntityEngine.Domain;
using Morph.EntityEngine.Ports;

namespace Morph.Infrastructure.Persistence.EntityEngine;

public sealed class EfStorageMappingStore : IStorageMappingStore
{
    private readonly PlatformDbContext _db;

    public EfStorageMappingStore(PlatformDbContext db)
    {
        _db = db;
    }

    public async Task<StorageMapping?> GetByVersionAsync(
        Guid versionId,
        CancellationToken cancellationToken = default)
    {
        var record = await _db.StorageMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.VersionId == versionId, cancellationToken)
            .ConfigureAwait(false);
        return record is null
            ? null
            : EntityEngineJson.Deserialize<StorageMapping>(record.MappingJson);
    }

    public async Task SaveAsync(StorageMapping mapping, CancellationToken cancellationToken = default)
    {
        var existing = await _db.StorageMappings
            .FirstOrDefaultAsync(m => m.VersionId == mapping.VersionId, cancellationToken)
            .ConfigureAwait(false);

        var json = EntityEngineJson.Serialize(mapping);
        if (existing is null)
        {
            _db.StorageMappings.Add(new StorageMappingRecord
            {
                VersionId = mapping.VersionId,
                DefinitionId = mapping.DefinitionId,
                EnvironmentId = mapping.EnvironmentId,
                SchemaName = mapping.SchemaName,
                TableName = mapping.TableName,
                Fingerprint = mapping.Fingerprint,
                MappingJson = json,
                MappedAt = mapping.MappedAt
            });
        }
        else
        {
            existing.SchemaName = mapping.SchemaName;
            existing.TableName = mapping.TableName;
            existing.Fingerprint = mapping.Fingerprint;
            existing.MappingJson = json;
            existing.MappedAt = mapping.MappedAt;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
