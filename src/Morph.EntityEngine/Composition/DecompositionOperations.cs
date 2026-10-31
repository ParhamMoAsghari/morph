using Morph.BuildingBlocks.Results;
using Morph.BuildingBlocks.Time;
using Morph.EntityEngine.Domain;
using Morph.EntityEngine.Ports;

namespace Morph.EntityEngine.Composition;

public sealed class DecompositionOperations
{
    private readonly IDefinitionStore _store;
    private readonly IClock _clock;

    public DecompositionOperations(IDefinitionStore store, IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    /// <summary>
    /// Extracts a field group into a new Component definition, preserving FieldIds
    /// (ownership moves; identity stays) unless <paramref name="forkFields"/> is true.
    /// </summary>
    public async Task<Result<DecompositionResult>> DecomposeAsync(
        Guid sourceVersionId,
        IReadOnlyList<Guid> fieldIdsToExtract,
        string newComponentSlug,
        string newComponentDisplayName,
        string embedAlias,
        bool forkFields = false,
        CancellationToken cancellationToken = default)
    {
        if (fieldIdsToExtract.Count == 0)
        {
            return Result.Failure<DecompositionResult>(Error.Validation("At least one FieldId is required."));
        }

        if (string.IsNullOrWhiteSpace(newComponentSlug) || string.IsNullOrWhiteSpace(embedAlias))
        {
            return Result.Failure<DecompositionResult>(
                Error.Validation("newComponentSlug and embedAlias are required."));
        }

        var source = await _store.GetVersionAsync(sourceVersionId, cancellationToken).ConfigureAwait(false);
        if (source is null)
        {
            return Result.Failure<DecompositionResult>(Error.NotFound("Source version was not found."));
        }

        if (!source.IsMutable)
        {
            return Result.Failure<DecompositionResult>(
                Error.Conflict("Cannot decompose a published version; create a new draft first."));
        }

        var extractSet = fieldIdsToExtract.ToHashSet();
        var extracted = source.Fields.Where(f => extractSet.Contains(f.FieldId)).ToList();
        if (extracted.Count != extractSet.Count)
        {
            var missing = extractSet.Except(extracted.Select(f => f.FieldId));
            return Result.Failure<DecompositionResult>(
                Error.Validation($"FieldIds not found on source: {string.Join(", ", missing)}"));
        }

        var slug = newComponentSlug.Trim().ToLowerInvariant();
        var existing = await _store.GetDefinitionBySlugAsync(source.EnvironmentId, slug, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return Result.Failure<DecompositionResult>(
                Error.Conflict($"Component slug '{slug}' already exists."));
        }

        if (source.ComponentUses.Any(c =>
                string.Equals(c.Alias, embedAlias.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure<DecompositionResult>(
                Error.Conflict($"ComponentUse alias '{embedAlias}' already exists on source."));
        }

        var now = _clock.UtcNow;
        var componentDefinitionId = Guid.NewGuid();
        var componentVersionId = Guid.NewGuid();

        var componentFields = extracted.Select(f =>
        {
            if (forkFields)
            {
                var clone = f.Clone();
                return new FieldDefinition
                {
                    FieldId = Guid.NewGuid(),
                    Slug = clone.Slug,
                    DisplayName = clone.DisplayName,
                    DataType = clone.DataType,
                    IsRequired = clone.IsRequired,
                    IsIndexed = clone.IsIndexed,
                    IsUnique = clone.IsUnique,
                    Encrypt = clone.Encrypt
                };
            }

            return f.Clone();
        }).ToList();

        var componentDefinition = new EntityDefinition
        {
            DefinitionId = componentDefinitionId,
            EnvironmentId = source.EnvironmentId,
            Slug = slug,
            DisplayName = newComponentDisplayName.Trim(),
            Kind = DefinitionKind.Component,
            CreatedAt = now
        };

        var componentVersion = new DefinitionVersion
        {
            VersionId = componentVersionId,
            DefinitionId = componentDefinitionId,
            EnvironmentId = source.EnvironmentId,
            Slug = slug,
            DisplayName = newComponentDisplayName.Trim(),
            Kind = DefinitionKind.Component,
            VersionNumber = 1,
            Status = DefinitionVersionStatus.Draft,
            Fields = componentFields,
            CreatedAt = now
        };

        // Remap source: remove extracted fields; add Embed ComponentUse pinning the new component draft.
        // Note: pin is to the draft version — publish order must publish component before (or with) parent.
        source.Fields = source.Fields.Where(f => !extractSet.Contains(f.FieldId)).ToList();
        source.ComponentUses.Add(new ComponentUse
        {
            ComponentUseId = Guid.NewGuid(),
            Alias = embedAlias.Trim(),
            TargetDefinitionId = componentDefinitionId,
            PinnedVersionId = componentVersionId,
            Mode = ComponentIncludeMode.Embed,
            IsRequired = true
        });

        await _store.SaveDefinitionAsync(componentDefinition, cancellationToken).ConfigureAwait(false);
        await _store.SaveVersionAsync(componentVersion, cancellationToken).ConfigureAwait(false);
        await _store.SaveVersionAsync(source, cancellationToken).ConfigureAwait(false);

        return Result.Success(new DecompositionResult(componentDefinition, componentVersion, source));
    }
}

public sealed record DecompositionResult(
    EntityDefinition ComponentDefinition,
    DefinitionVersion ComponentVersion,
    DefinitionVersion UpdatedSourceVersion);
