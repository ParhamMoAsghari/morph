using Morph.BuildingBlocks.Results;
using Morph.BuildingBlocks.Time;
using Morph.EntityEngine.Domain;
using Morph.EntityEngine.Ports;

namespace Morph.EntityEngine.Composition;

public sealed class CompositionOperations
{
    private readonly IDefinitionStore _store;
    private readonly IClock _clock;

    public CompositionOperations(IDefinitionStore store, IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    /// <summary>
    /// Creates a new draft definition that includes pinned component uses plus optional local fields.
    /// This is structural composition — not OOP inheritance.
    /// </summary>
    public async Task<Result<(EntityDefinition Definition, DefinitionVersion Version)>> ComposeAsync(
        Guid environmentId,
        string slug,
        string displayName,
        DefinitionKind kind,
        IReadOnlyList<ComponentUse> componentUses,
        IReadOnlyList<FieldDefinition>? localFields = null,
        IReadOnlyList<ConflictResolution>? conflictResolutions = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return Result.Failure<(EntityDefinition, DefinitionVersion)>(Error.Validation("Slug is required."));
        }

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var existing = await _store.GetDefinitionBySlugAsync(environmentId, normalizedSlug, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return Result.Failure<(EntityDefinition, DefinitionVersion)>(
                Error.Conflict($"Definition slug '{normalizedSlug}' already exists in this environment."));
        }

        foreach (var use in componentUses)
        {
            if (use.PinnedVersionId == Guid.Empty)
            {
                return Result.Failure<(EntityDefinition, DefinitionVersion)>(
                    Error.Validation($"ComponentUse '{use.Alias}' must pin an exact version."));
            }

            var pinned = await _store.GetVersionAsync(use.PinnedVersionId, cancellationToken).ConfigureAwait(false);
            if (pinned is null)
            {
                return Result.Failure<(EntityDefinition, DefinitionVersion)>(
                    Error.NotFound($"Pinned version '{use.PinnedVersionId}' was not found."));
            }

            if (pinned.DefinitionId != use.TargetDefinitionId)
            {
                return Result.Failure<(EntityDefinition, DefinitionVersion)>(
                    Error.Validation($"Pin for '{use.Alias}' does not match TargetDefinitionId."));
            }
        }

        var now = _clock.UtcNow;
        var definitionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var definition = new EntityDefinition
        {
            DefinitionId = definitionId,
            EnvironmentId = environmentId,
            Slug = normalizedSlug,
            DisplayName = displayName.Trim(),
            Kind = kind,
            CreatedAt = now
        };

        var version = new DefinitionVersion
        {
            VersionId = versionId,
            DefinitionId = definitionId,
            EnvironmentId = environmentId,
            Slug = normalizedSlug,
            DisplayName = displayName.Trim(),
            Kind = kind,
            VersionNumber = 1,
            Status = DefinitionVersionStatus.Draft,
            Fields = (localFields ?? []).Select(f => f.Clone()).ToList(),
            ComponentUses = componentUses.Select(c => c.Clone()).ToList(),
            ConflictResolutions = (conflictResolutions ?? []).Select(c => c.Clone()).ToList(),
            CreatedAt = now
        };

        await _store.SaveDefinitionAsync(definition, cancellationToken).ConfigureAwait(false);
        await _store.SaveVersionAsync(version, cancellationToken).ConfigureAwait(false);

        return Result.Success((definition, version));
    }
}
