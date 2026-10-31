namespace Morph.EntityEngine.Ports;

/// <summary>
/// Executes allowlisted DDL/DML against the application data plane (Postgres).
/// Implemented in Morph.Infrastructure with Npgsql.
/// </summary>
public interface IDdlExecutor
{
    Task EnsureSchemaAsync(string schemaName, CancellationToken cancellationToken = default);

    Task ExecuteAsync(string sql, CancellationToken cancellationToken = default);

    Task<bool> TableExistsAsync(
        string schemaName,
        string tableName,
        CancellationToken cancellationToken = default);

    Task<bool> ColumnExistsAsync(
        string schemaName,
        string tableName,
        string columnName,
        CancellationToken cancellationToken = default);
}
