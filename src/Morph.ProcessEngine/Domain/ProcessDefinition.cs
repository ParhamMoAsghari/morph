namespace Morph.ProcessEngine.Domain;

public sealed class ProcessDefinition
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

public sealed class ProcessDefinitionVersion
{
    public Guid VersionId { get; set; }

    public Guid DefinitionId { get; set; }

    public Guid EnvironmentId { get; set; }

    public required string Slug { get; set; }

    public required string DisplayName { get; set; }

    public int VersionNumber { get; set; }

    public ProcessVersionStatus Status { get; set; }

    /// <summary>Authoring IR (JSON canonical).</summary>
    public required string AuthoringJson { get; set; }

    /// <summary>Compiled graph JSON after publish.</summary>
    public string? CompiledJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public bool IsMutable => Status == ProcessVersionStatus.Draft;
}
