using Morph.DataPlane.Query;

namespace Morph.DataPlane.Records;

public sealed class EntityRecordDocument
{
    public required Guid Id { get; init; }

    public required Guid DefinitionVersionId { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public Guid? CreatedBy { get; init; }

    public Guid? UpdatedBy { get; init; }

    public long RowVersion { get; init; }

    /// <summary>Field path → value (effective model paths / slugs).</summary>
    public required IDictionary<string, object?> Fields { get; init; }
}

public sealed class DynamicListRequest
{
    public FilterNode? Filter { get; init; }

    public IReadOnlyList<SortSpec>? Sort { get; init; }

    public int PageSize { get; init; } = 50;

    /// <summary>Opaque cursor: base64(id) for keyset pagination on id.</summary>
    public string? Cursor { get; init; }
}
