namespace Morph.Access.Evaluation;

internal static class ResourcePatternMatcher
{
    /// <summary>
    /// Patterns: "entity:{slug}", "entity:{slug}/fields:{path}", "entity:*",
    /// "file:{bucket}", "file:*", "*".
    /// </summary>
    public static bool Matches(string pattern, string entityType, string? fieldPath)
    {
        if (string.IsNullOrWhiteSpace(pattern) || pattern == "*")
        {
            return true;
        }

        var p = pattern.Trim();
        if (p.Equals("entity:*", StringComparison.OrdinalIgnoreCase)
            || p.Equals("file:*", StringComparison.OrdinalIgnoreCase))
        {
            if (p.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                return entityType.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
                       || !entityType.Contains(':', StringComparison.Ordinal);
            }

            return true;
        }

        const string filePrefix = "file:";
        if (p.StartsWith(filePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var bucket = p[filePrefix.Length..];
            if (entityType.StartsWith(filePrefix, StringComparison.OrdinalIgnoreCase))
            {
                var actual = entityType[filePrefix.Length..];
                return bucket == "*" || string.Equals(bucket, actual, StringComparison.OrdinalIgnoreCase);
            }

            return bucket == "*" || string.Equals(bucket, entityType, StringComparison.OrdinalIgnoreCase);
        }

        const string entityPrefix = "entity:";
        if (!p.StartsWith(entityPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(p, entityType, StringComparison.OrdinalIgnoreCase);
        }

        var rest = p[entityPrefix.Length..];
        var fieldSep = rest.IndexOf("/fields:", StringComparison.OrdinalIgnoreCase);
        string slugPart;
        string? fieldPart = null;
        if (fieldSep >= 0)
        {
            slugPart = rest[..fieldSep];
            fieldPart = rest[(fieldSep + "/fields:".Length)..];
        }
        else
        {
            slugPart = rest;
        }

        if (slugPart != "*" && !string.Equals(slugPart, entityType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (fieldPart is null)
        {
            // Entity-level pattern matches entity and any field unless caller asks field-only match elsewhere.
            return true;
        }

        // Field-scoped pattern: at entity-level evaluation (no FieldPath) still match so
        // FieldMaskOnly obligations can be collected. When FieldPath is present, require equality.
        if (fieldPath is null)
        {
            return true;
        }

        return fieldPart == "*" || string.Equals(fieldPart, fieldPath, StringComparison.OrdinalIgnoreCase);
    }

    public static string? ExtractFieldPath(string pattern)
    {
        const string marker = "/fields:";
        var idx = pattern.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return idx < 0 ? null : pattern[(idx + marker.Length)..];
    }

    public static bool IsFieldScoped(string pattern) =>
        pattern.Contains("/fields:", StringComparison.OrdinalIgnoreCase);
}
