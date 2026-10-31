using Morph.BuildingBlocks.Results;
using Morph.EntityEngine.Domain;
using Morph.EntityEngine.Ports;
using Morph.EntityEngine.Storage;

namespace Morph.EntityEngine.Services;

/// <summary>
/// Facade: map EffectiveModel → StorageMapping, plan staged migration, apply/resume.
/// </summary>
public sealed class MigrationService
{
    private readonly IDefinitionStore _definitions;
    private readonly StorageMapper _mapper;
    private readonly MigrationPlanner _planner;
    private readonly StagedMigrationExecutor _executor;
    private readonly IStorageMappingStore _mappings;

    public MigrationService(
        IDefinitionStore definitions,
        StorageMapper mapper,
        MigrationPlanner planner,
        StagedMigrationExecutor executor,
        IStorageMappingStore mappings)
    {
        _definitions = definitions;
        _mapper = mapper;
        _planner = planner;
        _executor = executor;
        _mappings = mappings;
    }

    public StagedMigrationExecutor Executor => _executor;

    public async Task<Result<StorageMapping>> MapPublishedAsync(
        Guid versionId,
        string schemaName,
        CancellationToken cancellationToken = default)
    {
        var version = await _definitions.GetVersionAsync(versionId, cancellationToken).ConfigureAwait(false);
        if (version is null)
        {
            return Result.Failure<StorageMapping>(Error.NotFound("Definition version was not found."));
        }

        if (version.Status != DefinitionVersionStatus.Published
            && version.Status != DefinitionVersionStatus.Deprecated)
        {
            return Result.Failure<StorageMapping>(
                Error.Conflict("Only published (or deprecated) versions can be storage-mapped."));
        }

        if (version.EffectiveModel is null)
        {
            return Result.Failure<StorageMapping>(
                Error.Validation("Published version is missing EffectiveModel."));
        }

        if (version.Kind != DefinitionKind.Entity)
        {
            return Result.Failure<StorageMapping>(
                Error.Validation("Components do not receive hybrid storage tables."));
        }

        var mapping = _mapper.Map(version.EffectiveModel, schemaName);
        await _mappings.SaveAsync(mapping, cancellationToken).ConfigureAwait(false);
        return Result.Success(mapping);
    }

    public async Task<Result<(MigrationPlan Plan, StorageMapping Mapping)>> PlanCreateAsync(
        Guid versionId,
        string schemaName,
        CancellationToken cancellationToken = default)
    {
        var mapResult = await MapPublishedAsync(versionId, schemaName, cancellationToken).ConfigureAwait(false);
        if (mapResult.IsFailure)
        {
            return Result.Failure<(MigrationPlan, StorageMapping)>(mapResult.Error!);
        }

        var definition = await _definitions.GetDefinitionAsync(mapResult.Value.DefinitionId, cancellationToken)
            .ConfigureAwait(false);
        var plan = _planner.PlanCreate(mapResult.Value, definition?.CurrentPublishedVersionId);
        return Result.Success((plan, mapResult.Value));
    }

    public async Task<Result<(MigrationPlan Plan, StorageMapping Mapping)>> PlanEvolveAsync(
        Guid fromVersionId,
        Guid toVersionId,
        string schemaName,
        CancellationToken cancellationToken = default)
    {
        var from = await _mappings.GetByVersionAsync(fromVersionId, cancellationToken).ConfigureAwait(false);
        if (from is null)
        {
            var mappedFrom = await MapPublishedAsync(fromVersionId, schemaName, cancellationToken)
                .ConfigureAwait(false);
            if (mappedFrom.IsFailure)
            {
                return Result.Failure<(MigrationPlan, StorageMapping)>(mappedFrom.Error!);
            }

            from = mappedFrom.Value;
        }

        var toResult = await MapPublishedAsync(toVersionId, schemaName, cancellationToken).ConfigureAwait(false);
        if (toResult.IsFailure)
        {
            return Result.Failure<(MigrationPlan, StorageMapping)>(toResult.Error!);
        }

        var plan = _planner.PlanEvolve(from, toResult.Value);
        return Result.Success((plan, toResult.Value));
    }

    public Task<Result<MigrationRun>> ApplyAsync(
        MigrationPlan plan,
        Guid? priorPublishedVersionId = null,
        CancellationToken cancellationToken = default) =>
        _executor.StartAsync(plan, priorPublishedVersionId, cancellationToken);

    public Task<Result<MigrationRun>> ResumeAsync(
        Guid runId,
        CancellationToken cancellationToken = default) =>
        _executor.ResumeAsync(runId, cancellationToken);
}
