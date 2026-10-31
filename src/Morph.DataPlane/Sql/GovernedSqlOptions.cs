namespace Morph.DataPlane.Sql;

public sealed class GovernedSqlOptions
{
    public const string SectionName = "Morph:GovernedSql";

    /// <summary>Postgres statement_timeout applied via SET LOCAL (milliseconds).</summary>
    public int StatementTimeoutMs { get; set; } = 5_000;

    /// <summary>Hard cap on rows returned for SELECT (LIMIT injected when missing or higher).</summary>
    public int MaxRows { get; set; } = 500;

    /// <summary>Maximum SQL text length accepted from clients.</summary>
    public int MaxSqlLength { get; set; } = 8_192;
}
