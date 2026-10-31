using Morph.BuildingBlocks.Results;
using Morph.BuildingBlocks.Time;
using Morph.ProcessEngine.Domain;
using Morph.ProcessEngine.Ir;
using Morph.ProcessEngine.Ports;
using Morph.ProcessEngine.Publishing;

namespace Morph.ProcessEngine.Services;

public sealed class ProcessDefinitionService
{
    private readonly IProcessDefinitionStore _store;
    private readonly ProcessCompiler _compiler;
    private readonly IClock _clock;

    public ProcessDefinitionService(
        IProcessDefinitionStore store,
        ProcessCompiler compiler,
        IClock clock)
    {
        _store = store;
        _compiler = compiler;
        _clock = clock;
    }

    public Task<IReadOnlyList<ProcessDefinition>> ListAsync(
        Guid environmentId,
        CancellationToken cancellationToken = default) =>
        _store.ListDefinitionsAsync(environmentId, cancellationToken);

    public Task<ProcessDefinition?> GetDefinitionAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default) =>
        _store.GetDefinitionAsync(definitionId, cancellationToken);

    public Task<ProcessDefinitionVersion?> GetVersionAsync(
        Guid versionId,
        CancellationToken cancellationToken = default) =>
        _store.GetVersionAsync(versionId, cancellationToken);

    public async Task<Result<(ProcessDefinition Definition, ProcessDefinitionVersion Version)>> UpsertDraftAsync(
        Guid environmentId,
        Guid projectId,
        ProcessDocument document,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(document.Slug))
        {
            return Result.Failure<(ProcessDefinition, ProcessDefinitionVersion)>(
                Error.Validation("Process slug is required."));
        }

        var slug = document.Slug.Trim().ToLowerInvariant();
        var displayName = string.IsNullOrWhiteSpace(document.DisplayName) ? slug : document.DisplayName.Trim();
        var now = _clock.UtcNow;

        var existing = await _store.GetDefinitionBySlugAsync(environmentId, slug, cancellationToken)
            .ConfigureAwait(false);

        ProcessDefinition definition;
        ProcessDefinitionVersion version;

        if (existing is null)
        {
            definition = new ProcessDefinition
            {
                DefinitionId = document.DefinitionId is { } d && d != Guid.Empty ? d : Guid.NewGuid(),
                EnvironmentId = environmentId,
                ProjectId = projectId,
                Slug = slug,
                DisplayName = displayName,
                CreatedAt = now,
                UpdatedAt = now
            };

            version = new ProcessDefinitionVersion
            {
                VersionId = document.VersionId is { } v && v != Guid.Empty ? v : Guid.NewGuid(),
                DefinitionId = definition.DefinitionId,
                EnvironmentId = environmentId,
                Slug = slug,
                DisplayName = displayName,
                VersionNumber = 1,
                Status = ProcessVersionStatus.Draft,
                AuthoringJson = ProcessIrSerializer.ToJson(document),
                CreatedAt = now
            };

            await _store.SaveDefinitionAsync(definition, cancellationToken).ConfigureAwait(false);
            await _store.SaveVersionAsync(version, cancellationToken).ConfigureAwait(false);
            return Result.Success((definition, version));
        }

        definition = existing;
        definition.DisplayName = displayName;
        definition.UpdatedAt = now;

        var versions = await _store.ListVersionsAsync(definition.DefinitionId, cancellationToken)
            .ConfigureAwait(false);
        var draft = versions.FirstOrDefault(v => v.Status == ProcessVersionStatus.Draft);
        if (draft is null)
        {
            version = new ProcessDefinitionVersion
            {
                VersionId = Guid.NewGuid(),
                DefinitionId = definition.DefinitionId,
                EnvironmentId = environmentId,
                Slug = slug,
                DisplayName = displayName,
                VersionNumber = versions.Count == 0 ? 1 : versions.Max(v => v.VersionNumber) + 1,
                Status = ProcessVersionStatus.Draft,
                AuthoringJson = ProcessIrSerializer.ToJson(document),
                CreatedAt = now
            };
        }
        else
        {
            draft.DisplayName = displayName;
            draft.AuthoringJson = ProcessIrSerializer.ToJson(document);
            draft.CompiledJson = null;
            version = draft;
        }

        await _store.SaveDefinitionAsync(definition, cancellationToken).ConfigureAwait(false);
        await _store.SaveVersionAsync(version, cancellationToken).ConfigureAwait(false);
        return Result.Success((definition, version));
    }

    public async Task<Result<object>> ValidateAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        var version = await _store.GetVersionAsync(versionId, cancellationToken).ConfigureAwait(false);
        if (version is null)
        {
            return Result.Failure<object>(Error.NotFound("Process version not found."));
        }

        var document = ProcessIrSerializer.ParseJson(version.AuthoringJson);
        var compiled = _compiler.Compile(document, version.VersionId);
        if (compiled.IsFailure)
        {
            return Result.Failure<object>(compiled.Error!);
        }

        return Result.Success<object>(new { valid = true, stepCount = compiled.Value.Steps.Count });
    }

    public async Task<Result<ProcessDefinitionVersion>> PublishAsync(
        Guid versionId,
        CancellationToken cancellationToken = default)
    {
        var version = await _store.GetVersionAsync(versionId, cancellationToken).ConfigureAwait(false);
        if (version is null)
        {
            return Result.Failure<ProcessDefinitionVersion>(Error.NotFound("Process version not found."));
        }

        if (!version.IsMutable)
        {
            return Result.Failure<ProcessDefinitionVersion>(
                Error.Conflict("Only draft process versions can be published."));
        }

        var document = ProcessIrSerializer.ParseJson(version.AuthoringJson);
        var compiled = _compiler.Compile(document, version.VersionId);
        if (compiled.IsFailure)
        {
            return Result.Failure<ProcessDefinitionVersion>(compiled.Error!);
        }

        var now = _clock.UtcNow;
        version.Status = ProcessVersionStatus.Published;
        version.PublishedAt = now;
        version.CompiledJson = ProcessIrSerializer.ToJson(compiled.Value);
        await _store.SaveVersionAsync(version, cancellationToken).ConfigureAwait(false);

        var definition = await _store.GetDefinitionAsync(version.DefinitionId, cancellationToken)
            .ConfigureAwait(false);
        if (definition is not null)
        {
            definition.CurrentPublishedVersionId = version.VersionId;
            definition.UpdatedAt = now;
            await _store.SaveDefinitionAsync(definition, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(version);
    }

    public async Task<Result<ProcessDocument>> GetAuthoringAsync(
        Guid versionId,
        CancellationToken cancellationToken = default)
    {
        var version = await _store.GetVersionAsync(versionId, cancellationToken).ConfigureAwait(false);
        if (version is null)
        {
            return Result.Failure<ProcessDocument>(Error.NotFound("Process version not found."));
        }

        var doc = ProcessIrSerializer.ParseJson(version.AuthoringJson);
        doc.DefinitionId = version.DefinitionId;
        doc.VersionId = version.VersionId;
        return Result.Success(doc);
    }
}
