namespace Morph.DataPlane.Sql;

public sealed class GovernedSqlResult
{
    public required GovernedSqlKind Kind { get; init; }

    public required string PrimaryEntity { get; init; }

    /// <summary>Rows for SELECT; empty for DML.</summary>
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; } = [];

    /// <summary>Affected row count for DML; for SELECT equals Rows.Count.</summary>
    public int RowCount { get; init; }

    public bool Truncated { get; init; }
}
