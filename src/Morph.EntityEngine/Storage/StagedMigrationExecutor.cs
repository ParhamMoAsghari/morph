using System.Text.Json;
using System.Text.Json.Serialization;
using Morph.BuildingBlocks.Results;
using Morph.BuildingBlocks.Time;
using Morph.EntityEngine.Domain;
using Morph.EntityEngine.Ports;

namespace Morph.EntityEngine.Storage;

/// <summary>
/// Executes migration plans in stages: expand → backfill → constrain → swap.
/// Failure marks the run Failed and leaves the prior published pointer untouched.
/// Resume continues from <see cref="MigrationRun.LastCompletedStepOrdinal"/>.
/// </summary>
public sealed class StagedMigrationExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false
    };

    private readonly IDdlExecutor _ddl;
    private readonly IMigrationRunStore _runs;
    private readonly IDefinitionStore _definitions;
    private readonly IStorageMappingStore _mappings;
    private readonly IClock _clock;

    public StagedMigrationExecutor(
        IDdlExecutor ddl,
        IMigrationRunStore runs,
        IDefinitionStore definitions,
        IStorageMappingStore mappings,
        IClock clock)
    {
        _ddl = ddl;
        _runs = runs;
        _definitions = definitions;
        _mappings = mappings;
        _clock = clock;
    }

    /// <summary>
    /// Optional hook for tests: invoked before each step. Throw to simulate mid-run failure.
    /// </summary>
    public Func<MigrationRun, MigrationStep, CancellationToken, Task>? BeforeStepAsync { get; set; }

    public async Task<Result<MigrationRun>> StartAsync(
        MigrationPlan plan,
        Guid? priorPublishedVersionId = null,
        CancellationToken cancellationToken = default)
    {
        var definition = await _definitions.GetDefinitionAsync(plan.DefinitionId, cancellationToken)
            .ConfigureAwait(false);
        if (definition is null)
        {
            return Result.Failure<MigrationRun>(Error.NotFound("Definition was not found."));
        }

        var run = new MigrationRun
        {
            RunId = Guid.NewGuid(),
            PlanId = plan.PlanId,
            EnvironmentId = plan.EnvironmentId,
            DefinitionId = plan.DefinitionId,
            FromVersionId = plan.FromVersionId,
            ToVersionId = plan.ToVersionId,
            Status = MigrationRunStatus.Pending,
            CurrentStage = MigrationStage.Validate,
            LastCompletedStepOrdinal = -1,
            PlanJson = JsonSerializer.Serialize(plan, JsonOptions),
            PriorPublishedVersionId = priorPublishedVersionId ?? definition.CurrentPublishedVersionId,
            CreatedAt = _clock.UtcNow
        };

        await _runs.SaveAsync(run, cancellationToken).ConfigureAwait(false);
        return await ExecuteAsync(run.RunId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<MigrationRun>> ResumeAsync(
        Guid runId,
        CancellationToken cancellationToken = default) =>
        await ExecuteAsync(runId, cancellationToken).ConfigureAwait(false);

    private async Task<Result<MigrationRun>> ExecuteAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        var run = await _runs.GetAsync(runId, cancellationToken).ConfigureAwait(false);
        if (run is null)
        {
            return Result.Failure<MigrationRun>(Error.NotFound("Migration run was not found."));
        }

        if (run.Status is MigrationRunStatus.Completed or MigrationRunStatus.Cancelled)
        {
            return Result.Success(run);
        }

        MigrationPlan plan;
        try
        {
            plan = JsonSerializer.Deserialize<MigrationPlan>(run.PlanJson, JsonOptions)
                   ?? throw new InvalidOperationException("Plan JSON deserialized to null.");
        }
        catch (Exception ex)
        {
            return Result.Failure<MigrationRun>(
                Error.Validation($"Migration plan JSON is invalid: {ex.Message}"));
        }

        run.Status = MigrationRunStatus.Running;
        run.StartedAt ??= _clock.UtcNow;
        run.FailureMessage = null;
        run.FailureStage = null;
        await _runs.SaveAsync(run, cancellationToken).ConfigureAwait(false);

        // Capture prior pointer before any swap; never advance on failure.
        var definition = await _definitions.GetDefinitionAsync(run.DefinitionId, cancellationToken)
            .ConfigureAwait(false);
        if (definition is null)
        {
            return await FailAsync(run, MigrationStage.Validate, "Definition was not found.", cancellationToken)
                .ConfigureAwait(false);
        }

        var pointerBefore = definition.CurrentPublishedVersionId;

        foreach (var step in plan.Steps.OrderBy(s => s.Ordinal))
        {
            if (step.Ordinal <= run.LastCompletedStepOrdinal)
            {
                continue;
            }

            run.CurrentStage = step.Stage;
            await _runs.SaveAsync(run, cancellationToken).ConfigureAwait(false);

            try
            {
                if (BeforeStepAsync is not null)
                {
                    await BeforeStepAsync(run, step, cancellationToken).ConfigureAwait(false);
                }

                await ExecuteStepAsync(run, plan, definition, step, cancellationToken).ConfigureAwait(false);

                run.LastCompletedStepOrdinal = step.Ordinal;
                await _runs.SaveAsync(run, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Ensure prior pointer is intact.
                var fresh = await _definitions.GetDefinitionAsync(run.DefinitionId, cancellationToken)
                    .ConfigureAwait(false);
                if (fresh is not null
                    && fresh.CurrentPublishedVersionId != pointerBefore
                    && step.Kind != MigrationStepKind.SwapPointer)
                {
                    fresh.CurrentPublishedVersionId = pointerBefore;
                    await _definitions.SaveDefinitionAsync(fresh, cancellationToken).ConfigureAwait(false);
                }
                else if (fresh is not null
                         && step.Kind != MigrationStepKind.SwapPointer
                         && run.PriorPublishedVersionId != fresh.CurrentPublishedVersionId
                         && fresh.CurrentPublishedVersionId == run.ToVersionId)
                {
                    fresh.CurrentPublishedVersionId = run.PriorPublishedVersionId;
                    await _definitions.SaveDefinitionAsync(fresh, cancellationToken).ConfigureAwait(false);
                }

                return await FailAsync(run, step.Stage, ex.Message, cancellationToken).ConfigureAwait(false);
            }
        }

        run.Status = MigrationRunStatus.Completed;
        run.CompletedAt = _clock.UtcNow;
        run.CurrentStage = MigrationStage.Swap;
        await _runs.SaveAsync(run, cancellationToken).ConfigureAwait(false);
        await _mappings.SaveAsync(plan.TargetMapping, cancellationToken).ConfigureAwait(false);

        return Result.Success(run);
    }

    private async Task ExecuteStepAsync(
        MigrationRun run,
        MigrationPlan plan,
        EntityDefinition definition,
        MigrationStep step,
        CancellationToken cancellationToken)
    {
        switch (step.Kind)
        {
            case MigrationStepKind.NoOp:
            case MigrationStepKind.BackfillColumn when string.IsNullOrWhiteSpace(step.Sql):
                return;

            case MigrationStepKind.EnsureSchema:
            case MigrationStepKind.CreateTable:
            case MigrationStepKind.AddColumn:
            case MigrationStepKind.AddIndex:
            case MigrationStepKind.SetNotNull:
            case MigrationStepKind.AddUnique:
            case MigrationStepKind.BackfillColumn:
            case MigrationStepKind.DropColumn:
            case MigrationStepKind.DropIndex:
                if (string.IsNullOrWhiteSpace(step.Sql))
                {
                    throw new InvalidOperationException($"Step {step.Ordinal} ({step.Kind}) has empty SQL.");
                }

                await _ddl.ExecuteAsync(step.Sql, cancellationToken).ConfigureAwait(false);
                return;

            case MigrationStepKind.SwapPointer:
                // Only swap after expand/backfill/constrain succeeded.
                // Prior pointer remains until this step; failure before here leaves it unchanged.
                definition.CurrentPublishedVersionId = run.ToVersionId;
                await _definitions.SaveDefinitionAsync(definition, cancellationToken).ConfigureAwait(false);
                _ = plan;
                return;

            default:
                throw new InvalidOperationException($"Unsupported migration step kind: {step.Kind}");
        }
    }

    private async Task<Result<MigrationRun>> FailAsync(
        MigrationRun run,
        MigrationStage stage,
        string message,
        CancellationToken cancellationToken)
    {
        run.Status = MigrationRunStatus.Failed;
        run.FailureStage = stage.ToString();
        run.FailureMessage = message;
        run.CompletedAt = _clock.UtcNow;
        await _runs.SaveAsync(run, cancellationToken).ConfigureAwait(false);
        return Result.Failure<MigrationRun>(Error.Conflict(
            $"Migration failed at {stage}: {message}. Prior published pointer left unchanged."));
    }
}
