namespace Morph.EntityEngine.Domain;

/// <summary>
/// Authoring IR for a single definition version. Mutable while <see cref="DefinitionVersionStatus.Draft"/>;
/// immutable once published.
/// </summary>
public sealed class DefinitionVersion
{
    public required Guid VersionId { get; init; }

    public required Guid DefinitionId { get; init; }

    public required Guid EnvironmentId { get; init; }

    public required string Slug { get; set; }

    public required string DisplayName { get; set; }

    public required DefinitionKind Kind { get; set; }

    public int VersionNumber { get; set; } = 1;

    public DefinitionVersionStatus Status { get; set; } = DefinitionVersionStatus.Draft;

    public List<FieldDefinition> Fields { get; set; } = [];

    public List<ComponentUse> ComponentUses { get; set; } = [];

    public List<RelationshipDefinition> Relationships { get; set; } = [];

    public List<ConflictResolution> ConflictResolutions { get; set; } = [];

    /// <summary>Populated on publish; null while draft.</summary>
    public EffectiveModel? EffectiveModel { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public bool IsMutable => Status == DefinitionVersionStatus.Draft;

    public DefinitionVersion CloneAuthoring()
    {
        if (!IsMutable)
        {
            throw new InvalidOperationException("Published definition versions are immutable.");
        }

        return new DefinitionVersion
        {
            VersionId = VersionId,
            DefinitionId = DefinitionId,
            EnvironmentId = EnvironmentId,
            Slug = Slug,
            DisplayName = DisplayName,
            Kind = Kind,
            VersionNumber = VersionNumber,
            Status = Status,
            Fields = Fields.Select(f => f.Clone()).ToList(),
            ComponentUses = ComponentUses.Select(c => c.Clone()).ToList(),
            Relationships = Relationships.Select(r => r.Clone()).ToList(),
            ConflictResolutions = ConflictResolutions.Select(c => c.Clone()).ToList(),
            EffectiveModel = null,
            CreatedAt = CreatedAt,
            PublishedAt = PublishedAt
        };
    }
}
