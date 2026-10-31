using HotChocolate.Types;

namespace Morph.Runtime.Api.GraphQL;

public sealed class MorphRecordGraphql
{
    public required Guid Id { get; init; }

    public required string Entity { get; init; }

    public required Guid DefinitionVersionId { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public Guid? CreatedBy { get; init; }

    public Guid? UpdatedBy { get; init; }

    public long RowVersion { get; init; }

    [GraphQLType(typeof(AnyType))]
    public required IReadOnlyDictionary<string, object?> Fields { get; init; }
}

public sealed class MorphRecordConnectionGraphql
{
    public required IReadOnlyList<MorphRecordGraphql> Items { get; init; }

    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }
}
