namespace Morph.EntityEngine.Domain;

/// <summary>
/// Association between identities. Target version is an exact pin (no live inheritance).
/// </summary>
public sealed class RelationshipDefinition
{
    public required Guid RelationshipId { get; init; }

    public required string Slug { get; set; }

    public required string DisplayName { get; set; }

    public required Guid TargetDefinitionId { get; set; }

    /// <summary>Exact published (or draft-at-authoring) version pin.</summary>
    public required Guid PinnedVersionId { get; set; }

    public RelationshipCardinality Cardinality { get; set; } = RelationshipCardinality.One;

    public ComponentIncludeMode Mode { get; set; } = ComponentIncludeMode.Reference;

    public RelationshipDefinition Clone() => new()
    {
        RelationshipId = RelationshipId,
        Slug = Slug,
        DisplayName = DisplayName,
        TargetDefinitionId = TargetDefinitionId,
        PinnedVersionId = PinnedVersionId,
        Cardinality = Cardinality,
        Mode = Mode
    };
}
