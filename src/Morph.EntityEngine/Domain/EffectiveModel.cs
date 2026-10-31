namespace Morph.EntityEngine.Domain;

/// <summary>
/// Deterministic flattened model after composition resolution at publish.
/// </summary>
public sealed class EffectiveModel
{
    public required Guid DefinitionId { get; init; }

    public required Guid VersionId { get; init; }

    public required Guid EnvironmentId { get; init; }

    public required string Slug { get; init; }

    public required DefinitionKind Kind { get; init; }

    public required int VersionNumber { get; init; }

    public required IReadOnlyList<EffectiveField> Fields { get; init; }

    public required IReadOnlyList<EffectiveComponent> Components { get; init; }

    public required IReadOnlyList<EffectiveRelationship> Relationships { get; init; }

    public required IReadOnlyList<VersionPin> PinnedDependencies { get; init; }

    public required DateTimeOffset CompiledAt { get; init; }

    /// <summary>Stable fingerprint for determinism assertions.</summary>
    public required string Fingerprint { get; init; }
}

public sealed class EffectiveField
{
    public required Guid FieldId { get; init; }

    /// <summary>Dotted path e.g. "person.email" or local "employeeNumber".</summary>
    public required string Path { get; init; }

    public required string Slug { get; init; }

    public required string DisplayName { get; init; }

    public required FieldDataType DataType { get; init; }

    public bool IsRequired { get; init; }

    public bool IsIndexed { get; init; }

    public bool IsUnique { get; init; }

    public bool Encrypt { get; init; }

    public required Guid SourceDefinitionId { get; init; }

    public required Guid SourceVersionId { get; init; }

    /// <summary>Null for local fields; Embed/Reference for included fields.</summary>
    public ComponentIncludeMode? IncludeMode { get; init; }

    /// <summary>All composition paths that contributed this FieldId (diamond awareness).</summary>
    public required IReadOnlyList<string> SourcePaths { get; init; }
}

public sealed class EffectiveComponent
{
    public required Guid ComponentUseId { get; init; }

    public required string Alias { get; init; }

    public required Guid TargetDefinitionId { get; init; }

    public required Guid PinnedVersionId { get; init; }

    public required ComponentIncludeMode Mode { get; init; }

    public bool IsRequired { get; init; }
}

public sealed class EffectiveRelationship
{
    public required Guid RelationshipId { get; init; }

    public required string Slug { get; init; }

    public required Guid TargetDefinitionId { get; init; }

    public required Guid PinnedVersionId { get; init; }

    public required RelationshipCardinality Cardinality { get; init; }

    public required ComponentIncludeMode Mode { get; init; }
}

public sealed record VersionPin(Guid DefinitionId, Guid VersionId, string Slug, int VersionNumber);
