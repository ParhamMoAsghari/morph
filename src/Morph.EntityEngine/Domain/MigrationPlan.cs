namespace Morph.EntityEngine.Domain;

/// <summary>
/// Ordered DDL/data ops from storage mapping create or version N→N+1.
/// </summary>
public sealed class MigrationPlan
{
    public required Guid PlanId { get; init; }

    public required Guid EnvironmentId { get; init; }

    public required Guid DefinitionId { get; init; }

    public Guid? FromVersionId { get; init; }

    public required Guid ToVersionId { get; init; }

    public required StorageMapping TargetMapping { get; init; }

    public required IReadOnlyList<MigrationStep> Steps { get; init; }

    public required MigrationRiskClass RiskClass { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

public sealed class MigrationStep
{
    public required int Ordinal { get; init; }

    public required MigrationStage Stage { get; init; }

    public required MigrationStepKind Kind { get; init; }

    public required string Description { get; init; }

    /// <summary>Parameterized SQL template. Empty for pointer-swap / no-op steps.</summary>
    public string Sql { get; init; } = string.Empty;

    public bool IsDestructive { get; init; }
}

public enum MigrationStage
{
    Validate = 0,
    Expand = 1,
    Backfill = 2,
    Constrain = 3,
    Swap = 4,
    DropObsolete = 5
}

public enum MigrationStepKind
{
    EnsureSchema = 0,
    CreateTable = 1,
    AddColumn = 2,
    BackfillColumn = 3,
    AddIndex = 4,
    SetNotNull = 5,
    AddUnique = 6,
    SwapPointer = 7,
    DropColumn = 8,
    DropIndex = 9,
    NoOp = 10
}

public enum MigrationRiskClass
{
    Additive = 0,
    BackfillRequired = 1,
    Breaking = 2
}

/// <summary>
/// Persistent execution state for a staged migration. Failure leaves the prior published pointer.
/// </summary>
public sealed class MigrationRun
{
    public required Guid RunId { get; init; }

    public required Guid PlanId { get; init; }

    public required Guid EnvironmentId { get; init; }

    public required Guid DefinitionId { get; init; }

    public Guid? FromVersionId { get; set; }

    public required Guid ToVersionId { get; set; }

    public MigrationRunStatus Status { get; set; } = MigrationRunStatus.Pending;

    public MigrationStage CurrentStage { get; set; } = MigrationStage.Validate;

    /// <summary>Highest completed step ordinal (inclusive). -1 means none.</summary>
    public int LastCompletedStepOrdinal { get; set; } = -1;

    public string? FailureMessage { get; set; }

    public string? FailureStage { get; set; }

    /// <summary>Serialized <see cref="MigrationPlan"/> JSON for resume.</summary>
    public required string PlanJson { get; set; }

    /// <summary>
    /// Published version pointer before this run started. Restored semantics on failure:
    /// runtime stays on this version (never swapped to <see cref="ToVersionId"/>).
    /// </summary>
    public Guid? PriorPublishedVersionId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}

public enum MigrationRunStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}
