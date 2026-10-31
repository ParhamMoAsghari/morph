namespace Morph.DataPlane.Query;

public abstract record FilterNode;

public sealed record FilterAnd(IReadOnlyList<FilterNode> Children) : FilterNode;

public sealed record FilterOr(IReadOnlyList<FilterNode> Children) : FilterNode;

public sealed record FilterNot(FilterNode Child) : FilterNode;

public sealed record FilterComparison(
    string FieldPath,
    FilterOperator Operator,
    object? Value) : FilterNode;

public enum FilterOperator
{
    Eq,
    Neq,
    Gt,
    Gte,
    Lt,
    Lte,
    In,
    Contains,
    IsNull,
    IsNotNull
}

public sealed record SortSpec(string FieldPath, bool Descending = false);
