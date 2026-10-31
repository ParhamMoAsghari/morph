namespace Morph.EntityEngine.Domain;

/// <summary>
/// Hybrid Postgres table-per-entity mapping compiled from <see cref="EffectiveModel"/>.
/// Typed columns for indexed/unique/FK fields; JSONB for embeds and unindexed extras.
/// </summary>
public sealed class StorageMapping
{
    public required Guid DefinitionId { get; init; }

    public required Guid VersionId { get; init; }

    public required Guid EnvironmentId { get; init; }

    public required string SchemaName { get; init; }

    public required string TableName { get; init; }

    public required string QualifiedTableName { get; init; }

    public required IReadOnlyList<MappedColumn> Columns { get; init; }

    public required IReadOnlyList<MappedIndex> Indexes { get; init; }

    public required DateTimeOffset MappedAt { get; init; }

    public required string Fingerprint { get; init; }
}

public sealed class MappedColumn
{
    public required string ColumnName { get; init; }

    public required StorageColumnKind Kind { get; init; }

    public required string PostgresType { get; init; }

    public bool IsNullable { get; init; } = true;

    public bool IsPrimaryKey { get; init; }

    public Guid? FieldId { get; init; }

    public string? FieldPath { get; init; }

    public Guid? ComponentUseId { get; init; }

    public string? ComponentAlias { get; init; }

    public Guid? RelationshipId { get; init; }

    /// <summary>When true, NOT NULL / UNIQUE are applied in the constrain stage (not expand).</summary>
    public bool DeferConstraints { get; init; }
}

public enum StorageColumnKind
{
    System = 0,
    TypedField = 1,
    EmbedJsonb = 2,
    ExtrasJsonb = 3,
    ReferenceFk = 4
}

public sealed class MappedIndex
{
    public required string IndexName { get; init; }

    public required IReadOnlyList<string> ColumnNames { get; init; }

    public bool IsUnique { get; init; }
}
