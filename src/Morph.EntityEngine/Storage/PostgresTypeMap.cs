using Morph.EntityEngine.Domain;

namespace Morph.EntityEngine.Storage;

internal static class PostgresTypeMap
{
    public static string ForField(FieldDataType dataType) => dataType switch
    {
        FieldDataType.String => "text",
        FieldDataType.Int => "integer",
        FieldDataType.Long => "bigint",
        FieldDataType.Decimal => "numeric",
        FieldDataType.Bool => "boolean",
        FieldDataType.DateTime => "timestamptz",
        FieldDataType.Guid => "uuid",
        FieldDataType.Json => "jsonb",
        FieldDataType.File => "uuid",
        FieldDataType.FileArray => "uuid[]",
        _ => "jsonb"
    };
}
