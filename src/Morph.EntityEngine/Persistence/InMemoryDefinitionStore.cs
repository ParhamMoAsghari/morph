using System.Collections.Concurrent;
using Morph.EntityEngine.Domain;
using Morph.EntityEngine.Ports;

namespace Morph.EntityEngine.Persistence;

/// <summary>
/// Phase 1 persistence. Phase 2 will add EF tables + hybrid StorageMapping DDL.
/// </summary>
public sealed class InMemoryDefinitionStore : IDefinitionStore
{
    private readonly ConcurrentDictionary<Guid, EntityDefinition> _definitions = new();
    private readonly ConcurrentDictionary<Guid, DefinitionVersion> _versions = new();
    private readonly object _gate = new();

    public Task<EntityDefinition?> GetDefinitionAsync(Guid definitionId, CancellationToken cancellationToken = default)
    {
        _definitions.TryGetValue(definitionId, out var definition);
        return Task.FromResult(definition);
    }

    public Task<EntityDefinition?> GetDefinitionBySlugAsync(
        Guid environmentId,
        string slug,
        CancellationToken cancellationToken = default)
    {
        var match = _definitions.Values.FirstOrDefault(d =>
            d.EnvironmentId == environmentId
            && string.Equals(d.Slug, slug, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(match);
    }

    public Task<IReadOnlyList<EntityDefinition>> ListDefinitionsAsync(
        Guid environmentId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<EntityDefinition> list = _definitions.Values
            .Where(d => d.EnvironmentId == environmentId)
            .OrderBy(d => d.Slug, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<DefinitionVersion?> GetVersionAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        _versions.TryGetValue(versionId, out var version);
        return Task.FromResult(version);
    }

    public Task<IReadOnlyList<DefinitionVersion>> ListVersionsAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DefinitionVersion> list = _versions.Values
            .Where(v => v.DefinitionId == definitionId)
            .OrderBy(v => v.VersionNumber)
            .ToList();
        return Task.FromResult(list);
    }

    public Task SaveDefinitionAsync(EntityDefinition definition, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _definitions[definition.DefinitionId] = definition;
        }

        return Task.CompletedTask;
    }

    public Task SaveVersionAsync(DefinitionVersion version, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_versions.TryGetValue(version.VersionId, out var existing)
                && existing.Status == DefinitionVersionStatus.Published
                && !ReferenceEquals(existing, version))
            {
                // Defend against accidental mutation of a previously published snapshot.
                if (existing.Status != version.Status
                    || existing.EffectiveModel?.Fingerprint != version.EffectiveModel?.Fingerprint
                    || !AuthoringEquals(existing, version))
                {
                    // Allow status transitions only Draft→Published performed in-place on same instance,
                    // or identical re-save. Reject structural changes to published versions.
                    if (existing.Status == DefinitionVersionStatus.Published
                        && version.Status == DefinitionVersionStatus.Published
                        && !AuthoringEquals(existing, version))
                    {
                        throw new InvalidOperationException(
                            $"Published version '{version.VersionId}' is immutable.");
                    }
                }
            }

            _versions[version.VersionId] = version;
        }

        return Task.CompletedTask;
    }

    private static bool AuthoringEquals(DefinitionVersion a, DefinitionVersion b) =>
        a.Slug == b.Slug
        && a.DisplayName == b.DisplayName
        && a.Kind == b.Kind
        && a.VersionNumber == b.VersionNumber
        && a.Fields.Count == b.Fields.Count
        && a.ComponentUses.Count == b.ComponentUses.Count
        && a.Relationships.Count == b.Relationships.Count
        && a.Fields.Select(f => f.FieldId).SequenceEqual(b.Fields.Select(f => f.FieldId))
        && a.ComponentUses.Select(c => (c.ComponentUseId, c.PinnedVersionId))
            .SequenceEqual(b.ComponentUses.Select(c => (c.ComponentUseId, c.PinnedVersionId)));
}
