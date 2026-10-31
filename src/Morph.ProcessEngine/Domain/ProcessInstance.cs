namespace Morph.ProcessEngine.Domain;

public sealed class ProcessInstance
{
    public Guid InstanceId { get; set; }

    public Guid DefinitionId { get; set; }

    public Guid VersionId { get; set; }

    public Guid EnvironmentId { get; set; }

    public Guid ProjectId { get; set; }

    public required string Slug { get; set; }

    public ProcessInstanceStatus Status { get; set; }

    /// <summary>Process variables JSON object.</summary>
    public required string VariablesJson { get; set; }

    /// <summary>Cursor into compiled steps (JSON path / step id stack).</summary>
    public string? CursorJson { get; set; }

    public string? FailureMessage { get; set; }

    public DateTimeOffset? WaitingUntil { get; set; }

    public string? PrincipalId { get; set; }

    public string? PrincipalRolesJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class ProcessActivityLogEntry
{
    public Guid LogId { get; set; }

    public Guid InstanceId { get; set; }

    public required string StepId { get; set; }

    public required string ActivityType { get; set; }

    public required string Status { get; set; }

    public string? InputJson { get; set; }

    public string? OutputJson { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}
