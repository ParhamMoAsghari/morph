namespace Morph.Infrastructure.Persistence.EntityEngine;

public sealed class MigrationRunRecord
{
    public Guid RunId { get; set; }

    public Guid PlanId { get; set; }

    public Guid EnvironmentId { get; set; }

    public Guid DefinitionId { get; set; }

    public Guid? FromVersionId { get; set; }

    public Guid ToVersionId { get; set; }

    public required string Status { get; set; }

    public required string CurrentStage { get; set; }

    public int LastCompletedStepOrdinal { get; set; }

    public string? FailureMessage { get; set; }

    public string? FailureStage { get; set; }

    public required string PlanJson { get; set; }

    public Guid? PriorPublishedVersionId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}
