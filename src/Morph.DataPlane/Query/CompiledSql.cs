namespace Morph.DataPlane.Query;

public sealed class CompiledSql
{
    public required string Sql { get; init; }

    public required IReadOnlyList<Npgsql.NpgsqlParameter> Parameters { get; init; }
}
