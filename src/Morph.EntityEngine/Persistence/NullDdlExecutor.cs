using Morph.EntityEngine.Ports;

namespace Morph.EntityEngine.Persistence;

/// <summary>
/// Placeholder until Morph.Infrastructure registers <c>NpgsqlDdlExecutor</c>.
/// </summary>
public sealed class NullDdlExecutor : IDdlExecutor
{
    private static InvalidOperationException Error() =>
        new("No DDL executor is configured. Call AddMorphEntityEnginePersistence() from Morph.Infrastructure.");

    public Task EnsureSchemaAsync(string schemaName, CancellationToken cancellationToken = default) =>
        throw Error();

    public Task ExecuteAsync(string sql, CancellationToken cancellationToken = default) =>
        throw Error();

    public Task<bool> TableExistsAsync(
        string schemaName,
        string tableName,
        CancellationToken cancellationToken = default) =>
        throw Error();

    public Task<bool> ColumnExistsAsync(
        string schemaName,
        string tableName,
        string columnName,
        CancellationToken cancellationToken = default) =>
        throw Error();
}
