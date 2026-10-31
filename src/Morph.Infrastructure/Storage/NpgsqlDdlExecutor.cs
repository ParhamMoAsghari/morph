using System.Data;
using Microsoft.EntityFrameworkCore;
using Morph.EntityEngine.Ports;
using Morph.Infrastructure.Persistence;
using Npgsql;

namespace Morph.Infrastructure.Storage;

public sealed class NpgsqlDdlExecutor : IDdlExecutor
{
    private readonly PlatformDbContext _db;

    public NpgsqlDdlExecutor(PlatformDbContext db)
    {
        _db = db;
    }

    public Task EnsureSchemaAsync(string schemaName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);
        var quoted = schemaName.Replace("\"", "\"\"", StringComparison.Ordinal);
        return ExecuteAsync($"CREATE SCHEMA IF NOT EXISTS \"{quoted}\";", cancellationToken);
    }

    public async Task ExecuteAsync(string sql, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        await using var command = await CreateCommandAsync(sql, cancellationToken).ConfigureAwait(false);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> TableExistsAsync(
        string schemaName,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        await using var command = await CreateCommandAsync(
                """
                SELECT EXISTS (
                  SELECT 1
                  FROM information_schema.tables
                  WHERE table_schema = @schema AND table_name = @table
                );
                """,
                cancellationToken)
            .ConfigureAwait(false);

        command.Parameters.AddWithValue("schema", schemaName);
        command.Parameters.AddWithValue("table", tableName);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is true;
    }

    public async Task<bool> ColumnExistsAsync(
        string schemaName,
        string tableName,
        string columnName,
        CancellationToken cancellationToken = default)
    {
        await using var command = await CreateCommandAsync(
                """
                SELECT EXISTS (
                  SELECT 1
                  FROM information_schema.columns
                  WHERE table_schema = @schema AND table_name = @table AND column_name = @column
                );
                """,
                cancellationToken)
            .ConfigureAwait(false);

        command.Parameters.AddWithValue("schema", schemaName);
        command.Parameters.AddWithValue("table", tableName);
        command.Parameters.AddWithValue("column", columnName);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is true;
    }

    private async Task<NpgsqlCommand> CreateCommandAsync(string sql, CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
        if (connection is not NpgsqlConnection npgsql)
        {
            throw new InvalidOperationException("PlatformDbContext must use Npgsql.");
        }

        if (npgsql.State != ConnectionState.Open)
        {
            await npgsql.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        return new NpgsqlCommand(sql, npgsql);
    }
}
