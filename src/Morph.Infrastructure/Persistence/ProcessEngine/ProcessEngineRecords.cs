namespace Morph.Infrastructure.Persistence.ProcessEngine;

public sealed class ProcessDefinitionRecord
{
    public Guid DefinitionId { get; set; }

    public Guid EnvironmentId { get; set; }

    public Guid ProjectId { get; set; }

    public required string Slug { get; set; }

    public required string DisplayName { get; set; }

    public Guid? CurrentPublishedVersionId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ProcessVersionRecord
{
    public Guid VersionId { get; set; }

    public Guid DefinitionId { get; set; }

    public Guid EnvironmentId { get; set; }

    public required string Slug { get; set; }

    public required string DisplayName { get; set; }

    public int VersionNumber { get; set; }

    public required string Status { get; set; }

    public required string AuthoringJson { get; set; }

    public string? CompiledJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }
}

public sealed class ProcessInstanceRecord
{
    public Guid InstanceId { get; set; }

    public Guid DefinitionId { get; set; }

    public Guid VersionId { get; set; }

    public Guid EnvironmentId { get; set; }

    public Guid ProjectId { get; set; }

    public required string Slug { get; set; }

    public required string Status { get; set; }

    public required string VariablesJson { get; set; }

    public string? CursorJson { get; set; }

    public string? FailureMessage { get; set; }

    public DateTimeOffset? WaitingUntil { get; set; }

    public string? PrincipalId { get; set; }

    public string? PrincipalRolesJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class ProcessActivityLogRecord
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
