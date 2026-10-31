namespace Morph.EntityEngine.Domain;

/// <summary>
/// Stable definition identity with a lineage of versions.
/// </summary>
public sealed class EntityDefinition
{
    public required Guid DefinitionId { get; init; }

    public required Guid EnvironmentId { get; init; }

    public required string Slug { get; set; }

    public required string DisplayName { get; set; }

    public required DefinitionKind Kind { get; set; }

    /// <summary>Environment's current published pointer, if any.</summary>
    public Guid? CurrentPublishedVersionId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
