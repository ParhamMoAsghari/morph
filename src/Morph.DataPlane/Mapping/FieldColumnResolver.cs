using Morph.EntityEngine.Domain;

namespace Morph.DataPlane.Mapping;

public sealed class FieldColumnResolver
{
    private readonly StorageMapping _mapping;
    private readonly EffectiveModel _model;

    public FieldColumnResolver(StorageMapping mapping, EffectiveModel model)
    {
        _mapping = mapping;
        _model = model;
    }

    public StorageMapping Mapping => _mapping;

    public EffectiveModel Model => _model;

    public ResolvedColumn? Resolve(string fieldPath)
    {
        if (string.IsNullOrWhiteSpace(fieldPath))
        {
            return null;
        }

        var path = fieldPath.Trim();

        // System columns / aliases.
        if (path.Equals("id", StringComparison.OrdinalIgnoreCase))
        {
            return new ResolvedColumn("id", StorageColumnKind.System, IsJsonPath: false);
        }

        if (path.Equals("created_at", StringComparison.OrdinalIgnoreCase)
            || path.Equals("createdAt", StringComparison.OrdinalIgnoreCase))
        {
            return new ResolvedColumn("created_at", StorageColumnKind.System, IsJsonPath: false);
        }

        if (path.Equals("updated_at", StringComparison.OrdinalIgnoreCase)
            || path.Equals("updatedAt", StringComparison.OrdinalIgnoreCase))
        {
            return new ResolvedColumn("updated_at", StorageColumnKind.System, IsJsonPath: false);
        }

        if (path.Equals("created_by", StringComparison.OrdinalIgnoreCase)
            || path.Equals("createdBy", StringComparison.OrdinalIgnoreCase)
            || path.Equals("owner_id", StringComparison.OrdinalIgnoreCase)
            || path.Equals("ownerId", StringComparison.OrdinalIgnoreCase))
        {
            return new ResolvedColumn("created_by", StorageColumnKind.System, IsJsonPath: false);
        }

        if (path.Equals("definition_version_id", StringComparison.OrdinalIgnoreCase))
        {
            return new ResolvedColumn("definition_version_id", StorageColumnKind.System, IsJsonPath: false);
        }

        var typed = _mapping.Columns.FirstOrDefault(c =>
            c.Kind == StorageColumnKind.TypedField
            && c.FieldPath is not null
            && string.Equals(c.FieldPath, path, StringComparison.OrdinalIgnoreCase));

        if (typed is not null)
        {
            return new ResolvedColumn(typed.ColumnName, typed.Kind, IsJsonPath: false, typed.FieldId, typed.FieldPath);
        }

        // Embed JSONB: person.email → c_{useId}->>'email' (last segment)
        var field = _model.Fields.FirstOrDefault(f =>
            string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase));

        if (field is not null && field.IncludeMode == ComponentIncludeMode.Embed)
        {
            var alias = path.Contains('.') ? path.Split('.')[0] : null;
            var embedCol = _mapping.Columns.FirstOrDefault(c =>
                c.Kind == StorageColumnKind.EmbedJsonb
                && (alias is null
                    || string.Equals(c.ComponentAlias, alias, StringComparison.OrdinalIgnoreCase)));

            if (embedCol is not null)
            {
                var jsonKey = path.Contains('.') ? path[(path.IndexOf('.') + 1)..] : field.Slug;
                return new ResolvedColumn(
                    embedCol.ColumnName,
                    StorageColumnKind.EmbedJsonb,
                    IsJsonPath: true,
                    field.FieldId,
                    field.Path,
                    jsonKey);
            }
        }

        var extras = _mapping.Columns.FirstOrDefault(c => c.Kind == StorageColumnKind.ExtrasJsonb);
        if (extras is not null && field is not null)
        {
            return new ResolvedColumn(
                extras.ColumnName,
                StorageColumnKind.ExtrasJsonb,
                IsJsonPath: true,
                field.FieldId,
                field.Path,
                field.Slug);
        }

        // Slug-only match against local fields.
        var bySlug = _model.Fields.FirstOrDefault(f =>
            string.Equals(f.Slug, path, StringComparison.OrdinalIgnoreCase)
            && f.IncludeMode is null);

        if (bySlug is not null)
        {
            return Resolve(bySlug.Path);
        }

        return null;
    }

    public string ToSqlExpression(ResolvedColumn column)
    {
        var quoted = QuoteIdent(column.ColumnName);
        if (!column.IsJsonPath || column.JsonKey is null)
        {
            return quoted;
        }

        var key = column.JsonKey.Replace("'", "''", StringComparison.Ordinal);
        // text extract for comparisons; callers may cast.
        return $"{quoted}->>'{key}'";
    }

    public static string QuoteIdent(string ident) =>
        "\"" + ident.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}

public sealed record ResolvedColumn(
    string ColumnName,
    StorageColumnKind Kind,
    bool IsJsonPath,
    Guid? FieldId = null,
    string? FieldPath = null,
    string? JsonKey = null);
