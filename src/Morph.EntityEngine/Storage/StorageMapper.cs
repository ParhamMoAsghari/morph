using Morph.BuildingBlocks.Time;
using Morph.EntityEngine.Domain;

namespace Morph.EntityEngine.Storage;

/// <summary>
/// Compiles <see cref="EffectiveModel"/> → hybrid <see cref="StorageMapping"/>
/// (typed columns + JSONB embeds + extras).
/// </summary>
public sealed class StorageMapper
{
    private readonly IClock _clock;

    public StorageMapper(IClock clock)
    {
        _clock = clock;
    }

    public StorageMapping Map(EffectiveModel model, string schemaName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);

        if (model.Kind != DefinitionKind.Entity)
        {
            throw new InvalidOperationException(
                $"StorageMapping is only produced for Entity definitions (got {model.Kind}).");
        }

        var schema = PgIdent.Sanitize(schemaName);
        var table = "e_" + PgIdent.Sanitize(model.Slug);
        var columns = new List<MappedColumn>();
        var indexes = new List<MappedIndex>();

        AddSystemColumns(columns);

        foreach (var component in model.Components.Where(c => c.Mode == ComponentIncludeMode.Embed)
                     .OrderBy(c => c.Alias, StringComparer.Ordinal))
        {
            columns.Add(new MappedColumn
            {
                ColumnName = "c_" + PgIdent.ShortId(component.ComponentUseId),
                Kind = StorageColumnKind.EmbedJsonb,
                PostgresType = "jsonb",
                IsNullable = !component.IsRequired,
                ComponentUseId = component.ComponentUseId,
                ComponentAlias = component.Alias,
                DeferConstraints = component.IsRequired
            });
        }

        foreach (var component in model.Components.Where(c => c.Mode == ComponentIncludeMode.Reference)
                     .OrderBy(c => c.Alias, StringComparer.Ordinal))
        {
            var colName = "r_" + PgIdent.ShortId(component.ComponentUseId);
            columns.Add(new MappedColumn
            {
                ColumnName = colName,
                Kind = StorageColumnKind.ReferenceFk,
                PostgresType = "uuid",
                IsNullable = !component.IsRequired,
                ComponentUseId = component.ComponentUseId,
                ComponentAlias = component.Alias,
                DeferConstraints = component.IsRequired
            });

            indexes.Add(new MappedIndex
            {
                IndexName = $"ix_{table}_{colName}",
                ColumnNames = [colName],
                IsUnique = false
            });
        }

        foreach (var rel in model.Relationships
                     .Where(r => r.Mode == ComponentIncludeMode.Reference
                                 && r.Cardinality == RelationshipCardinality.One)
                     .OrderBy(r => r.Slug, StringComparer.Ordinal))
        {
            var colName = "fk_" + PgIdent.ShortId(rel.RelationshipId);
            columns.Add(new MappedColumn
            {
                ColumnName = colName,
                Kind = StorageColumnKind.ReferenceFk,
                PostgresType = "uuid",
                IsNullable = true,
                RelationshipId = rel.RelationshipId,
                FieldPath = rel.Slug,
                DeferConstraints = true
            });

            indexes.Add(new MappedIndex
            {
                IndexName = $"ix_{table}_{colName}",
                ColumnNames = [colName],
                IsUnique = false
            });
        }


        // Local / promoted typed columns: indexed, unique, or required non-embed fields.
        // Embed-sourced fields live in the component JSONB unless promoted via IsIndexed/IsUnique.
        var extrasNeeded = false;
        foreach (var field in model.Fields.OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            var isEmbedSourced = field.IncludeMode == ComponentIncludeMode.Embed;
            var promote = field.IsIndexed || field.IsUnique
                          || (!isEmbedSourced && (field.IsRequired || field.DataType is FieldDataType.File or FieldDataType.FileArray));

            if (isEmbedSourced && !promote)
            {
                continue;
            }

            if (!promote && !isEmbedSourced)
            {
                extrasNeeded = true;
                continue;
            }

            var colName = "f_" + PgIdent.ShortId(field.FieldId);
            columns.Add(new MappedColumn
            {
                ColumnName = colName,
                Kind = StorageColumnKind.TypedField,
                PostgresType = PostgresTypeMap.ForField(field.DataType),
                IsNullable = !field.IsRequired,
                FieldId = field.FieldId,
                FieldPath = field.Path,
                DeferConstraints = field.IsRequired || field.IsUnique
            });

            if (field.IsIndexed || field.IsUnique)
            {
                indexes.Add(new MappedIndex
                {
                    IndexName = $"ix_{table}_{colName}",
                    ColumnNames = [colName],
                    IsUnique = field.IsUnique
                });
            }
        }

        if (extrasNeeded || model.Fields.Any(f =>
                f.IncludeMode != ComponentIncludeMode.Embed
                && !f.IsIndexed
                && !f.IsUnique
                && !f.IsRequired
                && f.DataType is not (FieldDataType.File or FieldDataType.FileArray)))
        {
            columns.Add(new MappedColumn
            {
                ColumnName = "_extras",
                Kind = StorageColumnKind.ExtrasJsonb,
                PostgresType = "jsonb",
                IsNullable = false
            });
        }

        var fingerprint = PgIdent.Fingerprint(
            schema,
            table,
            string.Join(',', columns.Select(c =>
                $"{c.ColumnName}:{c.PostgresType}:{(c.IsNullable ? "n" : "1")}:{c.Kind}")),
            string.Join(',', indexes.Select(i =>
                $"{i.IndexName}:{string.Join('+', i.ColumnNames)}:{(i.IsUnique ? "u" : "i")}")));

        return new StorageMapping
        {
            DefinitionId = model.DefinitionId,
            VersionId = model.VersionId,
            EnvironmentId = model.EnvironmentId,
            SchemaName = schema,
            TableName = table,
            QualifiedTableName = PgIdent.Qualify(schema, table),
            Columns = columns,
            Indexes = indexes,
            MappedAt = _clock.UtcNow,
            Fingerprint = fingerprint
        };
    }

    private static void AddSystemColumns(List<MappedColumn> columns)
    {
        columns.Add(new MappedColumn
        {
            ColumnName = "id",
            Kind = StorageColumnKind.System,
            PostgresType = "uuid",
            IsNullable = false,
            IsPrimaryKey = true
        });
        columns.Add(new MappedColumn
        {
            ColumnName = "created_at",
            Kind = StorageColumnKind.System,
            PostgresType = "timestamptz",
            IsNullable = false
        });
        columns.Add(new MappedColumn
        {
            ColumnName = "updated_at",
            Kind = StorageColumnKind.System,
            PostgresType = "timestamptz",
            IsNullable = false
        });
        columns.Add(new MappedColumn
        {
            ColumnName = "created_by",
            Kind = StorageColumnKind.System,
            PostgresType = "uuid",
            IsNullable = true
        });
        columns.Add(new MappedColumn
        {
            ColumnName = "updated_by",
            Kind = StorageColumnKind.System,
            PostgresType = "uuid",
            IsNullable = true
        });
        columns.Add(new MappedColumn
        {
            ColumnName = "definition_version_id",
            Kind = StorageColumnKind.System,
            PostgresType = "uuid",
            IsNullable = false
        });
        columns.Add(new MappedColumn
        {
            ColumnName = "row_version",
            Kind = StorageColumnKind.System,
            PostgresType = "bigint",
            IsNullable = false
        });
    }
}
