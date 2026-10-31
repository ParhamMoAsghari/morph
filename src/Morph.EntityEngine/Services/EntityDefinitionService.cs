using Morph.BuildingBlocks.Results;
using Morph.BuildingBlocks.Time;
using Morph.EntityEngine.Composition;
using Morph.EntityEngine.Domain;
using Morph.EntityEngine.Ports;
using Morph.EntityEngine.Publishing;
using Morph.EntityEngine.Validation;

namespace Morph.EntityEngine.Services;

/// <summary>
/// Authoring facade: create/update drafts, validate, compose, decompose, publish, compile.
/// </summary>
public sealed class EntityDefinitionService
{
    private readonly IDefinitionStore _store;
    private readonly DefinitionGraphValidator _validator;
    private readonly EffectiveModelCompiler _compiler;
    private readonly CompositionOperations _composition;
    private readonly DecompositionOperations _decomposition;
    private readonly DefinitionPublisher _publisher;
    private readonly IClock _clock;

    public EntityDefinitionService(
        IDefinitionStore store,
        DefinitionGraphValidator validator,
        EffectiveModelCompiler compiler,
        CompositionOperations composition,
        DecompositionOperations decomposition,
        DefinitionPublisher publisher,
        IClock clock)
    {
        _store = store;
        _validator = validator;
        _compiler = compiler;
        _composition = composition;
        _decomposition = decomposition;
        _publisher = publisher;
        _clock = clock;
    }

    public Task<IReadOnlyList<EntityDefinition>> ListAsync(
        Guid environmentId,
        CancellationToken cancellationToken = default) =>
        _store.ListDefinitionsAsync(environmentId, cancellationToken);

    public Task<EntityDefinition?> GetDefinitionAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default) =>
        _store.GetDefinitionAsync(definitionId, cancellationToken);

    public Task<DefinitionVersion?> GetVersionAsync(
        Guid versionId,
        CancellationToken cancellationToken = default) =>
        _store.GetVersionAsync(versionId, cancellationToken);

    public async Task<Result<(EntityDefinition Definition, DefinitionVersion Version)>> CreateAsync(
        Guid environmentId,
        string slug,
        string displayName,
        DefinitionKind kind,
        IReadOnlyList<FieldDefinition>? fields = null,
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
                Error.Conflict($"Definition slug '{normalizedSlug}' already exists."));
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
            Fields = (fields ?? []).Select(f => f.Clone()).ToList(),
            CreatedAt = now
        };

        await _store.SaveDefinitionAsync(definition, cancellationToken).ConfigureAwait(false);
        await _store.SaveVersionAsync(version, cancellationToken).ConfigureAwait(false);
        return Result.Success((definition, version));
    }

    public async Task<Result<DefinitionVersion>> UpdateDraftAsync(
        Guid versionId,
        Action<DefinitionVersion> mutate,
        CancellationToken cancellationToken = default)
    {
        var version = await _store.GetVersionAsync(versionId, cancellationToken).ConfigureAwait(false);
        if (version is null)
        {
            return Result.Failure<DefinitionVersion>(Error.NotFound("Version not found."));
        }

        if (!version.IsMutable)
        {
            return Result.Failure<DefinitionVersion>(
                Error.Conflict("Published versions are immutable. Create a new draft to evolve."));
        }

        mutate(version);
        await _store.SaveVersionAsync(version, cancellationToken).ConfigureAwait(false);
        return Result.Success(version);
    }

    public Task<IReadOnlyList<DefinitionValidationIssue>> ValidateAsync(
        Guid versionId,
        CancellationToken cancellationToken = default) =>
        ValidateVersionAsync(versionId, cancellationToken);

    private async Task<IReadOnlyList<DefinitionValidationIssue>> ValidateVersionAsync(
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var version = await _store.GetVersionAsync(versionId, cancellationToken).ConfigureAwait(false);
        if (version is null)
        {
            return [new DefinitionValidationIssue("not_found", "Version not found.")];
        }

        return await _validator.ValidateAsync(version, cancellationToken).ConfigureAwait(false);
    }

    public Task<Result<(EntityDefinition Definition, DefinitionVersion Version)>> ComposeAsync(
        Guid environmentId,
        string slug,
        string displayName,
        DefinitionKind kind,
        IReadOnlyList<ComponentUse> componentUses,
        IReadOnlyList<FieldDefinition>? localFields = null,
        IReadOnlyList<ConflictResolution>? conflictResolutions = null,
        CancellationToken cancellationToken = default) =>
        _composition.ComposeAsync(
            environmentId,
            slug,
            displayName,
            kind,
            componentUses,
            localFields,
            conflictResolutions,
            cancellationToken);

    public Task<Result<DecompositionResult>> DecomposeAsync(
        Guid sourceVersionId,
        IReadOnlyList<Guid> fieldIdsToExtract,
        string newComponentSlug,
        string newComponentDisplayName,
        string embedAlias,
        bool forkFields = false,
        CancellationToken cancellationToken = default) =>
        _decomposition.DecomposeAsync(
            sourceVersionId,
            fieldIdsToExtract,
            newComponentSlug,
            newComponentDisplayName,
            embedAlias,
            forkFields,
            cancellationToken);

    public Task<Result<DefinitionVersion>> PublishAsync(
        Guid versionId,
        CancellationToken cancellationToken = default) =>
        _publisher.PublishAsync(versionId, requireDependenciesPublished: true, cancellationToken);

    public Task<Result<DefinitionVersion>> CreateDraftFromPublishedAsync(
        Guid publishedVersionId,
        CancellationToken cancellationToken = default) =>
        _publisher.CreateDraftFromPublishedAsync(publishedVersionId, cancellationToken);

    public async Task<Result<EffectiveModel>> CompileAsync(
        Guid versionId,
        CancellationToken cancellationToken = default)
    {
        var version = await _store.GetVersionAsync(versionId, cancellationToken).ConfigureAwait(false);
        if (version is null)
        {
            return Result.Failure<EffectiveModel>(Error.NotFound("Version not found."));
        }

        if (version.EffectiveModel is not null && version.Status == DefinitionVersionStatus.Published)
        {
            return Result.Success(version.EffectiveModel);
        }

        return await _compiler.CompileAsync(version, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Upgrades a draft's component pin to a newer published version of the same definition.
    /// Explicit only — never automatic when the source publishes.
    /// </summary>
    public async Task<Result<DefinitionVersion>> UpgradePinAsync(
        Guid draftVersionId,
        Guid componentUseId,
        Guid newPinnedVersionId,
        CancellationToken cancellationToken = default)
    {
        var draft = await _store.GetVersionAsync(draftVersionId, cancellationToken).ConfigureAwait(false);
        if (draft is null)
        {
            return Result.Failure<DefinitionVersion>(Error.NotFound("Draft version not found."));
        }

        if (!draft.IsMutable)
        {
            return Result.Failure<DefinitionVersion>(Error.Conflict("Cannot upgrade pins on a published version."));
        }

        var use = draft.ComponentUses.FirstOrDefault(c => c.ComponentUseId == componentUseId);
        if (use is null)
        {
            return Result.Failure<DefinitionVersion>(Error.NotFound("ComponentUse not found on draft."));
        }

        var newPin = await _store.GetVersionAsync(newPinnedVersionId, cancellationToken).ConfigureAwait(false);
        if (newPin is null)
        {
            return Result.Failure<DefinitionVersion>(Error.NotFound("New pinned version not found."));
        }

        if (newPin.DefinitionId != use.TargetDefinitionId)
        {
            return Result.Failure<DefinitionVersion>(
                Error.Validation("New pin must target the same definition as the ComponentUse."));
        }

        if (newPin.Status != DefinitionVersionStatus.Published
            && newPin.Status != DefinitionVersionStatus.Deprecated)
        {
            return Result.Failure<DefinitionVersion>(
                Error.Validation("Can only upgrade to a published (or deprecated) version pin."));
        }

        use.PinnedVersionId = newPinnedVersionId;
        await _store.SaveVersionAsync(draft, cancellationToken).ConfigureAwait(false);
        return Result.Success(draft);
    }
}
