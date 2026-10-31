namespace Morph.Infrastructure.Persistence.EntityEngine;

public sealed class DefinitionVersionRecord
{
    public Guid VersionId { get; set; }

    public Guid DefinitionId { get; set; }

    public Guid EnvironmentId { get; set; }

    public required string Slug { get; set; }

    public required string DisplayName { get; set; }

    public required string Kind { get; set; }

    public int VersionNumber { get; set; }

    public required string Status { get; set; }

    /// <summary>Authoring IR JSON: fields, componentUses, relationships, conflictResolutions.</summary>
    public required string AuthoringJson { get; set; }

    public string? EffectiveModelJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }
}
