namespace Morph.DataPlane.Sql;

public enum GovernedSqlKind
{
    Select,
    Insert,
    Update,
    Delete
}

/// <summary>
/// Parsed, allowlisted client SQL ready for rewrite/execution against published entity tables.
/// </summary>
public sealed class GovernedSqlStatement
{
    public required GovernedSqlKind Kind { get; init; }

    /// <summary>Primary entity slug (first / target table).</summary>
    public required string PrimaryEntitySlug { get; init; }

    /// <summary>All entity slugs referenced (FROM / JOIN / INTO / UPDATE target).</summary>
    public required IReadOnlyList<string> EntitySlugs { get; init; }

    /// <summary>
    /// Normalized SQL with entity table placeholders as <c>{{entity:slug}}</c>
    /// and client parameters preserved as <c>@name</c>.
    /// </summary>
    public required string TemplateSql { get; init; }

    /// <summary>Original table reference spans rewritten into placeholders (longest-first safe).</summary>
    public IReadOnlyList<string> OriginalTableRefs { get; init; } = [];

    /// <summary>Client-declared parameter names found in the SQL (@name).</summary>
    public required IReadOnlyList<string> ParameterNames { get; init; }

    /// <summary>True when the SELECT already contained a LIMIT clause.</summary>
    public bool HasLimit { get; init; }

    /// <summary>Client LIMIT value when present and parseable; otherwise null.</summary>
    public int? ClientLimit { get; init; }
}
