using Microsoft.EntityFrameworkCore;
using Morph.EntityEngine.Domain;
using Morph.EntityEngine.Ports;

namespace Morph.Infrastructure.Persistence.EntityEngine;

public sealed class EfDefinitionStore : IDefinitionStore
{
    private readonly PlatformDbContext _db;

    public EfDefinitionStore(PlatformDbContext db)
    {
        _db = db;
    }

    public async Task<EntityDefinition?> GetDefinitionAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default)
    {
        var record = await _db.EntityDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DefinitionId == definitionId, cancellationToken)
            .ConfigureAwait(false);
        return record is null ? null : ToDomain(record);
    }

    public async Task<EntityDefinition?> GetDefinitionBySlugAsync(
        Guid environmentId,
        string slug,
        CancellationToken cancellationToken = default)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        var record = await _db.EntityDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                d => d.EnvironmentId == environmentId && d.Slug == normalized,
                cancellationToken)
            .ConfigureAwait(false);
        return record is null ? null : ToDomain(record);
    }

    public async Task<IReadOnlyList<EntityDefinition>> ListDefinitionsAsync(
        Guid environmentId,
        CancellationToken cancellationToken = default)
    {
        var records = await _db.EntityDefinitions
            .AsNoTracking()
            .Where(d => d.EnvironmentId == environmentId)
            .OrderBy(d => d.Slug)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return records.Select(ToDomain).ToList();
    }

    public async Task<DefinitionVersion?> GetVersionAsync(
        Guid versionId,
        CancellationToken cancellationToken = default)
    {
        var record = await _db.DefinitionVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.VersionId == versionId, cancellationToken)
            .ConfigureAwait(false);
        return record is null ? null : ToDomain(record);
    }

    public async Task<IReadOnlyList<DefinitionVersion>> ListVersionsAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default)
    {
        var records = await _db.DefinitionVersions
            .AsNoTracking()
            .Where(v => v.DefinitionId == definitionId)
            .OrderBy(v => v.VersionNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return records.Select(ToDomain).ToList();
    }

    public async Task SaveDefinitionAsync(
        EntityDefinition definition,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.EntityDefinitions
            .FirstOrDefaultAsync(d => d.DefinitionId == definition.DefinitionId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            _db.EntityDefinitions.Add(new EntityDefinitionRecord
            {
                DefinitionId = definition.DefinitionId,
                EnvironmentId = definition.EnvironmentId,
                Slug = definition.Slug.Trim().ToLowerInvariant(),
                DisplayName = definition.DisplayName,
                Kind = definition.Kind.ToString(),
                CurrentPublishedVersionId = definition.CurrentPublishedVersionId,
                CreatedAt = definition.CreatedAt
            });
        }
        else
        {
            existing.Slug = definition.Slug.Trim().ToLowerInvariant();
            existing.DisplayName = definition.DisplayName;
            existing.Kind = definition.Kind.ToString();
            existing.CurrentPublishedVersionId = definition.CurrentPublishedVersionId;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveVersionAsync(
        DefinitionVersion version,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.DefinitionVersions
            .FirstOrDefaultAsync(v => v.VersionId == version.VersionId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null
            && existing.Status == nameof(DefinitionVersionStatus.Published)
            && version.Status == DefinitionVersionStatus.Published
            && existing.AuthoringJson != EntityEngineJson.SerializeAuthoring(version))
        {
            throw new InvalidOperationException(
                $"Published version '{version.VersionId}' is immutable.");
        }

        var authoring = EntityEngineJson.SerializeAuthoring(version);
        var effective = version.EffectiveModel is null
            ? null
            : EntityEngineJson.Serialize(version.EffectiveModel);

        if (existing is null)
        {
            _db.DefinitionVersions.Add(new DefinitionVersionRecord
            {
                VersionId = version.VersionId,
                DefinitionId = version.DefinitionId,
                EnvironmentId = version.EnvironmentId,
                Slug = version.Slug,
                DisplayName = version.DisplayName,
                Kind = version.Kind.ToString(),
                VersionNumber = version.VersionNumber,
                Status = version.Status.ToString(),
                AuthoringJson = authoring,
                EffectiveModelJson = effective,
                CreatedAt = version.CreatedAt,
                PublishedAt = version.PublishedAt
            });
        }
        else
        {
            existing.Slug = version.Slug;
            existing.DisplayName = version.DisplayName;
            existing.Kind = version.Kind.ToString();
            existing.VersionNumber = version.VersionNumber;
            existing.Status = version.Status.ToString();
            existing.AuthoringJson = authoring;
            existing.EffectiveModelJson = effective;
            existing.PublishedAt = version.PublishedAt;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static EntityDefinition ToDomain(EntityDefinitionRecord record) => new()
    {
        DefinitionId = record.DefinitionId,
        EnvironmentId = record.EnvironmentId,
        Slug = record.Slug,
        DisplayName = record.DisplayName,
        Kind = Enum.Parse<DefinitionKind>(record.Kind, ignoreCase: true),
        CurrentPublishedVersionId = record.CurrentPublishedVersionId,
        CreatedAt = record.CreatedAt
    };

    private static DefinitionVersion ToDomain(DefinitionVersionRecord record)
    {
        var version = new DefinitionVersion
        {
            VersionId = record.VersionId,
            DefinitionId = record.DefinitionId,
            EnvironmentId = record.EnvironmentId,
            Slug = record.Slug,
            DisplayName = record.DisplayName,
            Kind = Enum.Parse<DefinitionKind>(record.Kind, ignoreCase: true),
            VersionNumber = record.VersionNumber,
            Status = Enum.Parse<DefinitionVersionStatus>(record.Status, ignoreCase: true),
            CreatedAt = record.CreatedAt,
            PublishedAt = record.PublishedAt
        };

        EntityEngineJson.ApplyAuthoring(version, record.AuthoringJson);
        if (!string.IsNullOrWhiteSpace(record.EffectiveModelJson))
        {
            version.EffectiveModel = EntityEngineJson.Deserialize<EffectiveModel>(record.EffectiveModelJson);
        }

        return version;
    }
}
