using Morph.BuildingBlocks.Results;
using Morph.BuildingBlocks.Time;
using Morph.EntityEngine.Composition;
using Morph.EntityEngine.Domain;
using Morph.EntityEngine.Ports;

namespace Morph.EntityEngine.Publishing;

public sealed class DefinitionPublisher
{
    private readonly IDefinitionStore _store;
    private readonly EffectiveModelCompiler _compiler;
    private readonly IClock _clock;

    public DefinitionPublisher(
        IDefinitionStore store,
        EffectiveModelCompiler compiler,
        IClock clock)
    {
        _store = store;
        _compiler = compiler;
        _clock = clock;
    }

    /// <summary>
    /// draft → published (immutable). Compiles EffectiveModel and freezes exact dependency pins.
    /// Does not silently upgrade pins when dependency definitions publish newer versions.
    /// </summary>
    public async Task<Result<DefinitionVersion>> PublishAsync(
        Guid versionId,
        bool requireDependenciesPublished = true,
        CancellationToken cancellationToken = default)
    {
        var version = await _store.GetVersionAsync(versionId, cancellationToken).ConfigureAwait(false);
        if (version is null)
        {
            return Result.Failure<DefinitionVersion>(Error.NotFound("Definition version was not found."));
        }

        if (version.Status != DefinitionVersionStatus.Draft)
        {
            return Result.Failure<DefinitionVersion>(
                Error.Conflict($"Only draft versions can be published. Current status: {version.Status}."));
        }

        if (requireDependenciesPublished)
        {
            var depResult = await EnsureDependenciesPublishedAsync(version, cancellationToken).ConfigureAwait(false);
            if (depResult.IsFailure)
            {
                return Result.Failure<DefinitionVersion>(depResult.Error!);
            }
        }

        var compileResult = await _compiler.CompileAsync(version, cancellationToken).ConfigureAwait(false);
        if (compileResult.IsFailure)
        {
            return Result.Failure<DefinitionVersion>(compileResult.Error!);
        }

        version.EffectiveModel = compileResult.Value;
        version.Status = DefinitionVersionStatus.Published;
        version.PublishedAt = _clock.UtcNow;

        await _store.SaveVersionAsync(version, cancellationToken).ConfigureAwait(false);

        var definition = await _store.GetDefinitionAsync(version.DefinitionId, cancellationToken).ConfigureAwait(false);
        if (definition is not null)
        {
            // Entities: runtime pointer advances only on staged migration Swap (fail-safe).
            // Components: no hybrid table; pointer may advance immediately on publish.
            if (version.Kind == DefinitionKind.Component)
            {
                definition.CurrentPublishedVersionId = version.VersionId;
            }

            definition.Slug = version.Slug;
            definition.DisplayName = version.DisplayName;
            definition.Kind = version.Kind;
            await _store.SaveDefinitionAsync(definition, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(version);
    }

    /// <summary>
    /// Creates a new draft from a published version (for evolution / pin upgrades).
    /// Copied pins remain exact — caller must explicitly bump pins to upgrade.
    /// </summary>
    public async Task<Result<DefinitionVersion>> CreateDraftFromPublishedAsync(
        Guid publishedVersionId,
        CancellationToken cancellationToken = default)
    {
        var published = await _store.GetVersionAsync(publishedVersionId, cancellationToken).ConfigureAwait(false);
        if (published is null)
        {
            return Result.Failure<DefinitionVersion>(Error.NotFound("Published version was not found."));
        }

        if (published.Status != DefinitionVersionStatus.Published
            && published.Status != DefinitionVersionStatus.Deprecated)
        {
            return Result.Failure<DefinitionVersion>(
                Error.Conflict("Can only branch a draft from a published or deprecated version."));
        }

        var versions = await _store.ListVersionsAsync(published.DefinitionId, cancellationToken).ConfigureAwait(false);
        if (versions.Any(v => v.Status == DefinitionVersionStatus.Draft))
        {
            return Result.Failure<DefinitionVersion>(
                Error.Conflict("A draft already exists for this definition."));
        }

        var nextNumber = versions.Max(v => v.VersionNumber) + 1;
        var draft = new DefinitionVersion
        {
            VersionId = Guid.NewGuid(),
            DefinitionId = published.DefinitionId,
            EnvironmentId = published.EnvironmentId,
            Slug = published.Slug,
            DisplayName = published.DisplayName,
            Kind = published.Kind,
            VersionNumber = nextNumber,
            Status = DefinitionVersionStatus.Draft,
            Fields = published.Fields.Select(f => f.Clone()).ToList(),
            ComponentUses = published.ComponentUses.Select(c => c.Clone()).ToList(),
            Relationships = published.Relationships.Select(r => r.Clone()).ToList(),
            ConflictResolutions = published.ConflictResolutions.Select(c => c.Clone()).ToList(),
            CreatedAt = _clock.UtcNow
        };

        await _store.SaveVersionAsync(draft, cancellationToken).ConfigureAwait(false);
        return Result.Success(draft);
    }

    private async Task<Result> EnsureDependenciesPublishedAsync(
        DefinitionVersion version,
        CancellationToken cancellationToken)
    {
        foreach (var use in version.ComponentUses)
        {
            var pinned = await _store.GetVersionAsync(use.PinnedVersionId, cancellationToken).ConfigureAwait(false);
            if (pinned is null)
            {
                return Result.Failure(Error.NotFound(
                    $"Pinned dependency version '{use.PinnedVersionId}' for '{use.Alias}' was not found."));
            }

            if (pinned.Status != DefinitionVersionStatus.Published
                && pinned.Status != DefinitionVersionStatus.Deprecated)
            {
                return Result.Failure(Error.Validation(
                    $"Dependency '{use.Alias}' pin '{use.PinnedVersionId}' is not published (status: {pinned.Status}). Publish dependencies first."));
            }
        }

        foreach (var rel in version.Relationships)
        {
            var pinned = await _store.GetVersionAsync(rel.PinnedVersionId, cancellationToken).ConfigureAwait(false);
            if (pinned is null)
            {
                return Result.Failure(Error.NotFound(
                    $"Pinned relationship version '{rel.PinnedVersionId}' for '{rel.Slug}' was not found."));
            }

            if (pinned.Status != DefinitionVersionStatus.Published
                && pinned.Status != DefinitionVersionStatus.Deprecated)
            {
                return Result.Failure(Error.Validation(
                    $"Relationship '{rel.Slug}' pin is not published (status: {pinned.Status})."));
            }
        }

        return Result.Success();
    }
}
