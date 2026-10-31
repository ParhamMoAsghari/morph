using System.Globalization;
using System.Text;
using System.Text.Json;
using Morph.BuildingBlocks.Paging;
using Morph.BuildingBlocks.Time;
using Morph.DataPlane.Cache;
using Morph.DataPlane.Mapping;
using Morph.DataPlane.Query;
using Morph.EntityEngine.Domain;
using Npgsql;
using NpgsqlTypes;

namespace Morph.DataPlane.Records;

public sealed class NpgsqlDynamicRecordRepository : IDynamicRecordRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly NpgsqlDataSource _dataSource;
    private readonly IClock _clock;

    public NpgsqlDynamicRecordRepository(NpgsqlDataSource dataSource, IClock clock)
    {
        _dataSource = dataSource;
        _clock = clock;
    }

    public async Task<EntityRecordDocument> InsertAsync(
        PublishedEntityModel model,
        IReadOnlyDictionary<string, object?> fields,
        Guid? actorId,
        CancellationToken cancellationToken = default)
    {
        var resolver = new FieldColumnResolver(model.StorageMapping, model.EffectiveModel);
        var id = Guid.NewGuid();
        var now = _clock.UtcNow;
        var assignments = BuildWriteAssignments(resolver, model, fields, forInsert: true);

        var quotedColumns = new List<string>
        {
            "\"id\"", "\"created_at\"", "\"updated_at\"", "\"created_by\"", "\"updated_by\"",
            "\"definition_version_id\"", "\"row_version\""
        };
        var quotedValues = new List<string>
        {
            "@id", "@created_at", "@updated_at", "@created_by", "@updated_by",
            "@definition_version_id", "@row_version"
        };

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Uuid) { Value = id });
        command.Parameters.Add(new NpgsqlParameter("created_at", NpgsqlDbType.TimestampTz) { Value = now });
        command.Parameters.Add(new NpgsqlParameter("updated_at", NpgsqlDbType.TimestampTz) { Value = now });
        command.Parameters.Add(new NpgsqlParameter("created_by", NpgsqlDbType.Uuid)
        {
            Value = (object?)actorId ?? DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("updated_by", NpgsqlDbType.Uuid)
        {
            Value = (object?)actorId ?? DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("definition_version_id", NpgsqlDbType.Uuid)
        {
            Value = model.Version.VersionId
        });
        command.Parameters.Add(new NpgsqlParameter("row_version", NpgsqlDbType.Bigint) { Value = 1L });

        var i = 0;
        foreach (var (columnName, value, isJson) in assignments)
        {
            var pname = "c" + i++;
            quotedColumns.Add(FieldColumnResolver.QuoteIdent(columnName));
            quotedValues.Add("@" + pname);
            if (isJson)
            {
                command.Parameters.Add(new NpgsqlParameter(pname, NpgsqlDbType.Jsonb)
                {
                    Value = value is null
                        ? "{}"
                        : value is string s
                            ? s
                            : JsonSerializer.Serialize(value, JsonOptions)
                });
            }
            else
            {
                AddValueParam(command, pname, value);
            }
        }

        command.CommandText =
            $"INSERT INTO {model.StorageMapping.QualifiedTableName} ({string.Join(", ", quotedColumns)}) " +
            $"VALUES ({string.Join(", ", quotedValues)})";

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return (await GetAsync(model, id, obligationFilter: null, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<EntityRecordDocument?> GetAsync(
        PublishedEntityModel model,
        Guid id,
        FilterNode? obligationFilter,
        CancellationToken cancellationToken = default)
    {
        var resolver = new FieldColumnResolver(model.StorageMapping, model.EffectiveModel);
        var compiler = new FilterCompiler(resolver);
        var idFilter = new FilterComparison("id", FilterOperator.Eq, id);
        FilterNode filter = obligationFilter is null
            ? idFilter
            : new FilterAnd([idFilter, obligationFilter]);

        var compiled = compiler.Compile(filter);
        if (compiled.IsFailure)
        {
            throw new InvalidOperationException(compiled.Error!.Message);
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT * FROM {model.StorageMapping.QualifiedTableName} WHERE {compiled.Value.Sql} LIMIT 1";
        foreach (var p in compiled.Value.Parameters)
        {
            command.Parameters.Add(p);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return MapRow(reader, model);
    }

    public async Task<PageResult<EntityRecordDocument>> ListAsync(
        PublishedEntityModel model,
        DynamicListRequest request,
        FilterNode? obligationFilter,
        CancellationToken cancellationToken = default)
    {
        var resolver = new FieldColumnResolver(model.StorageMapping, model.EffectiveModel);
        var compiler = new FilterCompiler(resolver);

        FilterNode? filter = request.Filter;
        if (obligationFilter is not null)
        {
            filter = filter is null ? obligationFilter : new FilterAnd([filter, obligationFilter]);
        }

        if (!string.IsNullOrWhiteSpace(request.Cursor) && Guid.TryParse(DecodeCursor(request.Cursor), out var cursorId))
        {
            var cursorFilter = new FilterComparison("id", FilterOperator.Lt, cursorId);
            filter = filter is null ? cursorFilter : new FilterAnd([filter, cursorFilter]);
        }

        var where = compiler.Compile(filter);
        if (where.IsFailure)
        {
            throw new InvalidOperationException(where.Error!.Message);
        }

        var order = compiler.CompileSort(request.Sort);
        if (order.IsFailure)
        {
            throw new InvalidOperationException(order.Error!.Message);
        }

        var pageSize = Math.Clamp(
            request.PageSize <= 0 ? PageRequest.DefaultPageSize : request.PageSize,
            1,
            PageRequest.MaxPageSize);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT * FROM {model.StorageMapping.QualifiedTableName} WHERE {where.Value.Sql} " +
            $"ORDER BY {order.Value.Sql} LIMIT {pageSize + 1}";

        foreach (var p in where.Value.Parameters)
        {
            command.Parameters.Add(p);
        }

        var items = new List<EntityRecordDocument>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            items.Add(MapRow(reader, model));
        }

        var hasMore = items.Count > pageSize;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        string? next = hasMore && items.Count > 0 ? EncodeCursor(items[^1].Id.ToString("N")) : null;
        return new PageResult<EntityRecordDocument>(items, next, hasMore);
    }

    public async Task<EntityRecordDocument?> UpdateAsync(
        PublishedEntityModel model,
        Guid id,
        IReadOnlyDictionary<string, object?> fields,
        Guid? actorId,
        FilterNode? obligationFilter,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(model, id, obligationFilter, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        var resolver = new FieldColumnResolver(model.StorageMapping, model.EffectiveModel);
        var assignments = BuildWriteAssignments(resolver, model, fields, forInsert: false);
        if (assignments.Count == 0)
        {
            return existing;
        }

        var compiler = new FilterCompiler(resolver);
        var idFilter = new FilterComparison("id", FilterOperator.Eq, id);
        FilterNode filter = obligationFilter is null
            ? idFilter
            : new FilterAnd([idFilter, obligationFilter]);
        var compiled = compiler.Compile(filter);
        if (compiled.IsFailure)
        {
            throw new InvalidOperationException(compiled.Error!.Message);
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();

        var sets = new List<string>
        {
            "\"updated_at\" = @updated_at",
            "\"updated_by\" = @updated_by",
            "\"row_version\" = \"row_version\" + 1"
        };
        command.Parameters.Add(new NpgsqlParameter("updated_at", NpgsqlDbType.TimestampTz) { Value = _clock.UtcNow });
        command.Parameters.Add(new NpgsqlParameter("updated_by", NpgsqlDbType.Uuid)
        {
            Value = (object?)actorId ?? DBNull.Value
        });

        var i = 0;
        foreach (var (columnName, value, isJson) in assignments)
        {
            var pname = "u" + i++;
            sets.Add($"{FieldColumnResolver.QuoteIdent(columnName)} = @{pname}");
            if (isJson)
            {
                command.Parameters.Add(new NpgsqlParameter(pname, NpgsqlDbType.Jsonb)
                {
                    Value = value is null
                        ? "{}"
                        : value is string s
                            ? s
                            : JsonSerializer.Serialize(value, JsonOptions)
                });
            }
            else
            {
                AddValueParam(command, pname, value);
            }
        }

        foreach (var p in compiled.Value.Parameters)
        {
            command.Parameters.Add(new NpgsqlParameter("w" + p.ParameterName, p.NpgsqlDbType) { Value = p.Value });
        }

        var whereSql = compiled.Value.Sql;
        foreach (var p in compiled.Value.Parameters)
        {
            whereSql = whereSql.Replace("@" + p.ParameterName, "@w" + p.ParameterName, StringComparison.Ordinal);
        }

        command.CommandText =
            $"UPDATE {model.StorageMapping.QualifiedTableName} SET {string.Join(", ", sets)} WHERE {whereSql}";

        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (affected == 0)
        {
            return null;
        }

        return await GetAsync(model, id, obligationFilter, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(
        PublishedEntityModel model,
        Guid id,
        FilterNode? obligationFilter,
        CancellationToken cancellationToken = default)
    {
        var resolver = new FieldColumnResolver(model.StorageMapping, model.EffectiveModel);
        var compiler = new FilterCompiler(resolver);
        var idFilter = new FilterComparison("id", FilterOperator.Eq, id);
        FilterNode filter = obligationFilter is null
            ? idFilter
            : new FilterAnd([idFilter, obligationFilter]);
        var compiled = compiler.Compile(filter);
        if (compiled.IsFailure)
        {
            throw new InvalidOperationException(compiled.Error!.Message);
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"DELETE FROM {model.StorageMapping.QualifiedTableName} WHERE {compiled.Value.Sql}";
        foreach (var p in compiled.Value.Parameters)
        {
            command.Parameters.Add(p);
        }

        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected > 0;
    }

    private static List<(string ColumnName, object? Value, bool IsJson)> BuildWriteAssignments(
        FieldColumnResolver resolver,
        PublishedEntityModel model,
        IReadOnlyDictionary<string, object?> fields,
        bool forInsert)
    {
        var typed = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var embeds = new Dictionary<string, Dictionary<string, object?>>(StringComparer.OrdinalIgnoreCase);
        var extras = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in fields)
        {
            if (key.Equals("id", StringComparison.OrdinalIgnoreCase)
                || key.Equals("created_by", StringComparison.OrdinalIgnoreCase)
                || key.Equals("owner_id", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var resolved = resolver.Resolve(key);
            var field = model.EffectiveModel.Fields.FirstOrDefault(f =>
                string.Equals(f.Path, key, StringComparison.OrdinalIgnoreCase)
                || string.Equals(f.Slug, key, StringComparison.OrdinalIgnoreCase));

            if (resolved is { Kind: StorageColumnKind.TypedField, IsJsonPath: false })
            {
                typed[resolved.ColumnName] = Coerce(value, field?.DataType);
                continue;
            }

            if (resolved is { Kind: StorageColumnKind.EmbedJsonb, IsJsonPath: true, JsonKey: not null })
            {
                if (!embeds.TryGetValue(resolved.ColumnName, out var doc))
                {
                    doc = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    embeds[resolved.ColumnName] = doc;
                }

                doc[resolved.JsonKey] = value;
                continue;
            }

            if (field is not null
                || resolved is { Kind: StorageColumnKind.ExtrasJsonb })
            {
                extras[field?.Slug ?? key] = value;
            }
        }

        var result = new List<(string, object?, bool)>();
        foreach (var (col, val) in typed)
        {
            result.Add((col, val, false));
        }

        foreach (var (col, doc) in embeds)
        {
            result.Add((col, doc, true));
        }

        var extrasCol = model.StorageMapping.Columns.FirstOrDefault(c => c.Kind == StorageColumnKind.ExtrasJsonb);
        if (extrasCol is not null)
        {
            if (extras.Count > 0 || forInsert)
            {
                result.Add((extrasCol.ColumnName, extras.Count > 0 ? extras : new Dictionary<string, object?>(), true));
            }
        }

        return result;
    }

    private static object? Coerce(object? value, FieldDataType? dataType)
    {
        if (value is null || dataType is null)
        {
            return value;
        }

        return dataType switch
        {
            FieldDataType.Guid when value is string s && Guid.TryParse(s, out var g) => g,
            FieldDataType.Int when value is string s
                && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) => i,
            FieldDataType.Long when value is string s
                && long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) => l,
            FieldDataType.Bool when value is string s && bool.TryParse(s, out var b) => b,
            FieldDataType.DateTime when value is string s
                && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto) => dto,
            _ => value
        };
    }

    private static void AddValueParam(NpgsqlCommand command, string name, object? value)
    {
        if (value is null)
        {
            command.Parameters.Add(new NpgsqlParameter(name, DBNull.Value));
            return;
        }

        command.Parameters.Add(value switch
        {
            Guid g => new NpgsqlParameter(name, NpgsqlDbType.Uuid) { Value = g },
            bool b => new NpgsqlParameter(name, NpgsqlDbType.Boolean) { Value = b },
            int i => new NpgsqlParameter(name, NpgsqlDbType.Integer) { Value = i },
            long l => new NpgsqlParameter(name, NpgsqlDbType.Bigint) { Value = l },
            decimal d => new NpgsqlParameter(name, NpgsqlDbType.Numeric) { Value = d },
            DateTimeOffset dto => new NpgsqlParameter(name, NpgsqlDbType.TimestampTz) { Value = dto },
            DateTime dt => new NpgsqlParameter(name, NpgsqlDbType.TimestampTz) { Value = dt },
            string s => new NpgsqlParameter(name, NpgsqlDbType.Text) { Value = s },
            _ => new NpgsqlParameter(name, value)
        });
    }

    private static EntityRecordDocument MapRow(NpgsqlDataReader reader, PublishedEntityModel model)
    {
        var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var resolver = new FieldColumnResolver(model.StorageMapping, model.EffectiveModel);

        var id = reader.GetGuid(reader.GetOrdinal("id"));
        var createdAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at"));
        var updatedAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("updated_at"));
        Guid? createdBy = reader.IsDBNull(reader.GetOrdinal("created_by"))
            ? null
            : reader.GetGuid(reader.GetOrdinal("created_by"));
        Guid? updatedBy = reader.IsDBNull(reader.GetOrdinal("updated_by"))
            ? null
            : reader.GetGuid(reader.GetOrdinal("updated_by"));
        var versionId = reader.GetGuid(reader.GetOrdinal("definition_version_id"));
        var rowVersion = reader.GetInt64(reader.GetOrdinal("row_version"));

        foreach (var field in model.EffectiveModel.Fields)
        {
            var resolved = resolver.Resolve(field.Path);
            if (resolved is null)
            {
                continue;
            }

            if (resolved.IsJsonPath)
            {
                var ordinal = reader.GetOrdinal(resolved.ColumnName);
                if (reader.IsDBNull(ordinal))
                {
                    continue;
                }

                var json = reader.GetString(ordinal);
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
                if (resolved.JsonKey is not null
                    && doc.RootElement.TryGetProperty(resolved.JsonKey, out var el))
                {
                    fields[field.Path] = JsonElementToObject(el);
                }
                else if (resolved.Kind == StorageColumnKind.ExtrasJsonb
                         && doc.RootElement.TryGetProperty(field.Slug, out var extrasEl))
                {
                    fields[field.Path] = JsonElementToObject(extrasEl);
                }
            }
            else
            {
                var ordinal = reader.GetOrdinal(resolved.ColumnName);
                fields[field.Path] = reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);
            }
        }

        fields["created_by"] = createdBy;
        fields["owner_id"] = createdBy;

        return new EntityRecordDocument
        {
            Id = id,
            DefinitionVersionId = versionId,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            CreatedBy = createdBy,
            UpdatedBy = updatedBy,
            RowVersion = rowVersion,
            Fields = fields
        };
    }

    private static object? JsonElementToObject(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number when el.TryGetInt64(out var l) => l,
        JsonValueKind.Number when el.TryGetDecimal(out var d) => d,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Object or JsonValueKind.Array => el.GetRawText(),
        _ => el.ToString()
    };

    private static string EncodeCursor(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static string DecodeCursor(string cursor)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
        }
        catch
        {
            return cursor;
        }
    }
}
