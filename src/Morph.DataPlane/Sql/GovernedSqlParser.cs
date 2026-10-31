using System.Text;
using System.Text.RegularExpressions;
using Morph.BuildingBlocks.Results;

namespace Morph.DataPlane.Sql;

/// <summary>
/// Allowlists a Postgres-compatible SQL subset: single SELECT/INSERT/UPDATE/DELETE
/// against published entity tables (by slug or e_{slug}). Denies DDL and privilege abuse.
/// </summary>
public static partial class GovernedSqlParser
{
    private static readonly HashSet<string> ForbiddenKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "CREATE", "DROP", "ALTER", "TRUNCATE", "GRANT", "REVOKE", "COPY", "RESET", "SHOW",
        "VACUUM", "ANALYZE", "CLUSTER", "REINDEX", "COMMENT", "CALL", "DO", "EXECUTE", "PERFORM",
        "PREPARE", "DEALLOCATE", "LISTEN", "NOTIFY", "UNLISTEN", "LOCK", "CHECKPOINT", "LOAD",
        "IMPORT", "EXPORT", "REFRESH", "REASSIGN", "DISCARD", "BEGIN", "COMMIT", "ROLLBACK",
        "SAVEPOINT", "RELEASE", "START", "ABORT", "DECLARE", "FETCH", "MOVE", "CLOSE", "CURSOR",
        "FUNCTION", "PROCEDURE", "TRIGGER", "RULE", "POLICY", "EXTENSION", "PUBLICATION",
        "SUBSCRIPTION", "SECURITY", "OWNER", "INTO", // bare INTO (select into) blocked; INSERT INTO handled
        "PG_CATALOG", "INFORMATION_SCHEMA", "PG_SLEEP", "PG_READ_FILE", "PG_WRITE_FILE",
        "LO_IMPORT", "LO_EXPORT", "DBLINK", "DBLINK_EXEC", "SET_CONFIG", "CURRENT_SETTING"
    };

    public static Result<GovernedSqlStatement> Parse(string sql, int maxSqlLength = 8192)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return Result.Failure<GovernedSqlStatement>(Error.Validation("SQL is required."));
        }

        if (sql.Length > maxSqlLength)
        {
            return Result.Failure<GovernedSqlStatement>(
                Error.Validation($"SQL exceeds max length of {maxSqlLength} characters."));
        }

        var stripped = StripComments(sql).Trim();
        if (stripped.Length == 0)
        {
            return Result.Failure<GovernedSqlStatement>(Error.Validation("SQL is empty after removing comments."));
        }

        // Single statement only (allow one optional trailing semicolon).
        var withoutTrailing = stripped.TrimEnd().TrimEnd(';').TrimEnd();
        if (withoutTrailing.Contains(';'))
        {
            return Result.Failure<GovernedSqlStatement>(
                Error.Validation("Multiple SQL statements are not allowed."));
        }

        var upper = withoutTrailing.ToUpperInvariant();
        if (ContainsForbidden(upper, withoutTrailing))
        {
            return Result.Failure<GovernedSqlStatement>(
                Error.Validation("SQL contains a denied keyword, catalog, or privilege operation."));
        }

        GovernedSqlKind kind;
        if (upper.StartsWith("SELECT", StringComparison.Ordinal))
        {
            kind = GovernedSqlKind.Select;
        }
        else if (upper.StartsWith("INSERT", StringComparison.Ordinal))
        {
            kind = GovernedSqlKind.Insert;
        }
        else if (upper.StartsWith("UPDATE", StringComparison.Ordinal))
        {
            kind = GovernedSqlKind.Update;
        }
        else if (upper.StartsWith("DELETE", StringComparison.Ordinal))
        {
            kind = GovernedSqlKind.Delete;
        }
        else if (upper.StartsWith("SET", StringComparison.Ordinal))
        {
            return Result.Failure<GovernedSqlStatement>(
                Error.Validation("SET statements are not allowed."));
        }
        else
        {
            return Result.Failure<GovernedSqlStatement>(
                Error.Validation("Only SELECT, INSERT, UPDATE, and DELETE statements are allowlisted."));
        }

        // INSERT INTO is required; reject SELECT ... INTO.
        if (kind == GovernedSqlKind.Select
            && SelectIntoPattern().IsMatch(withoutTrailing))
        {
            return Result.Failure<GovernedSqlStatement>(
                Error.Validation("SELECT INTO is not allowed."));
        }

        if (kind == GovernedSqlKind.Insert && !upper.StartsWith("INSERT INTO", StringComparison.Ordinal))
        {
            return Result.Failure<GovernedSqlStatement>(
                Error.Validation("INSERT must use INSERT INTO."));
        }

        var tables = ExtractTableRefs(kind, withoutTrailing);
        if (tables.Count == 0)
        {
            return Result.Failure<GovernedSqlStatement>(
                Error.Validation("Could not resolve a target entity table from the SQL."));
        }

        var slugs = new List<string>();
        var slugByRef = new List<(string TableRef, string Slug)>();
        foreach (var table in tables)
        {
            if (!TryNormalizeEntitySlug(table, out var slug))
            {
                return Result.Failure<GovernedSqlStatement>(
                    Error.Validation($"Table reference '{table}' is not an allowlisted entity table form."));
            }

            slugByRef.Add((table, slug));
            if (!slugs.Contains(slug, StringComparer.OrdinalIgnoreCase))
            {
                slugs.Add(slug);
            }
        }

        var template = RewriteTablesToPlaceholders(withoutTrailing, slugByRef);
        var paramNames = ExtractParameterNames(withoutTrailing);

        int? clientLimit = null;
        var hasLimit = false;
        if (kind == GovernedSqlKind.Select)
        {
            var limitMatch = LimitPattern().Match(withoutTrailing);
            if (limitMatch.Success)
            {
                hasLimit = true;
                if (int.TryParse(limitMatch.Groups[1].Value, out var lim))
                {
                    clientLimit = lim;
                }
            }
        }

        return Result.Success(new GovernedSqlStatement
        {
            Kind = kind,
            PrimaryEntitySlug = slugs[0],
            EntitySlugs = slugs,
            TemplateSql = template,
            OriginalTableRefs = slugByRef.Select(x => x.TableRef).ToList(),
            ParameterNames = paramNames,
            HasLimit = hasLimit,
            ClientLimit = clientLimit
        });
    }

    public static bool TryNormalizeEntitySlug(string tableRef, out string slug)
    {
        slug = "";
        var t = tableRef.Trim().Trim('"').Trim();
        if (t.Length == 0)
        {
            return false;
        }

        // Drop optional schema prefix (env schema enforced at rewrite time).
        var dot = t.LastIndexOf('.');
        if (dot >= 0)
        {
            t = t[(dot + 1)..].Trim('"');
        }

        if (t.StartsWith("e_", StringComparison.OrdinalIgnoreCase))
        {
            slug = t[2..].ToLowerInvariant();
            return slug.Length > 0 && IsSafeIdent(slug);
        }

        slug = t.ToLowerInvariant();
        return IsSafeIdent(slug);
    }

    private static bool IsSafeIdent(string s) =>
        s.Length is > 0 and <= 64
        && s.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');

    private static IReadOnlyList<string> ExtractTableRefs(GovernedSqlKind kind, string sql)
    {
        var list = new List<string>();
        switch (kind)
        {
            case GovernedSqlKind.Select:
            case GovernedSqlKind.Delete:
                foreach (Match m in FromJoinPattern().Matches(sql))
                {
                    list.Add(m.Groups["table"].Value);
                }

                break;
            case GovernedSqlKind.Insert:
                var insert = InsertIntoPattern().Match(sql);
                if (insert.Success)
                {
                    list.Add(insert.Groups["table"].Value);
                }

                break;
            case GovernedSqlKind.Update:
                var update = UpdatePattern().Match(sql);
                if (update.Success)
                {
                    list.Add(update.Groups["table"].Value);
                }

                foreach (Match m in FromJoinPattern().Matches(sql))
                {
                    list.Add(m.Groups["table"].Value);
                }

                break;
        }

        return list;
    }

    private static string RewriteTablesToPlaceholders(
        string sql,
        IReadOnlyList<(string TableRef, string Slug)> refs)
    {
        // Replace extracted table refs (longest first). Only exact extracted spellings are rewritten
        // so string literals like 'note' are left alone when the FROM clause used e_note / schema.e_note.
        var result = sql;
        foreach (var (tableRef, slug) in refs
                     .DistinctBy(r => r.TableRef, StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(r => r.TableRef.Length))
        {
            var pattern = $@"(?<![A-Za-z0-9_]){Regex.Escape(tableRef)}(?![A-Za-z0-9_])";
            result = Regex.Replace(
                result,
                pattern,
                $"{{{{entity:{slug}}}}}",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return result;
    }

    private static IReadOnlyList<string> ExtractParameterNames(string sql)
    {
        var names = new List<string>();
        foreach (Match m in ParamPattern().Matches(sql))
        {
            var name = m.Groups[1].Value;
            if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(name);
            }
        }

        return names;
    }

    private static bool ContainsForbidden(string upper, string original)
    {
        // Word-boundary check for keywords; also block schema-qualified system catalogs in original.
        foreach (var kw in ForbiddenKeywords)
        {
            if (kw is "INTO")
            {
                // INSERT INTO is allowed; SELECT INTO / COPY INTO blocked via other checks.
                continue;
            }

            if (KeywordBoundary(upper, kw))
            {
                // SET is forbidden always; CREATE FUNCTION etc. already covered.
                return true;
            }
        }

        if (original.Contains("pg_catalog", StringComparison.OrdinalIgnoreCase)
            || original.Contains("information_schema", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Deny session/GUC SET (UPDATE ... SET col = ... remains allowed — "SET" is not a banned keyword).
        if (Regex.IsMatch(upper, @"^\s*SET\b")
            || Regex.IsMatch(upper, @"\bSET\s+(ROLE|SESSION|LOCAL|TRANSACTION)\b"))
        {
            return true;
        }

        return false;
    }

    private static bool KeywordBoundary(string upperHaystack, string keyword)
    {
        var idx = 0;
        while ((idx = upperHaystack.IndexOf(keyword, idx, StringComparison.Ordinal)) >= 0)
        {
            var beforeOk = idx == 0 || !IsIdentChar(upperHaystack[idx - 1]);
            var after = idx + keyword.Length;
            var afterOk = after >= upperHaystack.Length || !IsIdentChar(upperHaystack[after]);
            if (beforeOk && afterOk)
            {
                return true;
            }

            idx = after;
        }

        return false;
    }

    private static bool IsIdentChar(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';

    private static string StripComments(string sql)
    {
        var sb = new StringBuilder(sql.Length);
        var i = 0;
        var inSingle = false;
        var inDouble = false;
        while (i < sql.Length)
        {
            var c = sql[i];
            if (!inDouble && c == '\'' && !inSingle)
            {
                inSingle = true;
                sb.Append(c);
                i++;
                continue;
            }

            if (inSingle)
            {
                sb.Append(c);
                if (c == '\'' && i + 1 < sql.Length && sql[i + 1] == '\'')
                {
                    sb.Append(sql[i + 1]);
                    i += 2;
                    continue;
                }

                if (c == '\'')
                {
                    inSingle = false;
                }

                i++;
                continue;
            }

            if (!inSingle && c == '"' && !inDouble)
            {
                inDouble = true;
                sb.Append(c);
                i++;
                continue;
            }

            if (inDouble)
            {
                sb.Append(c);
                if (c == '"')
                {
                    inDouble = false;
                }

                i++;
                continue;
            }

            if (c == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                while (i < sql.Length && sql[i] is not ('\n' or '\r'))
                {
                    i++;
                }

                continue;
            }

            if (c == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < sql.Length && !(sql[i] == '*' && sql[i + 1] == '/'))
                {
                    i++;
                }

                i = Math.Min(i + 2, sql.Length);
                continue;
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }

    [GeneratedRegex(@"\bFROM\s+(?<table>(?:""[^""]+""|[A-Za-z_][A-Za-z0-9_]*)(?:\.(?:""[^""]+""|[A-Za-z_][A-Za-z0-9_]*))?)|\bJOIN\s+(?<table>(?:""[^""]+""|[A-Za-z_][A-Za-z0-9_]*)(?:\.(?:""[^""]+""|[A-Za-z_][A-Za-z0-9_]*))?)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FromJoinPattern();

    [GeneratedRegex(@"\bINSERT\s+INTO\s+(?<table>(?:""[^""]+""|[A-Za-z_][A-Za-z0-9_]*)(?:\.(?:""[^""]+""|[A-Za-z_][A-Za-z0-9_]*))?)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InsertIntoPattern();

    [GeneratedRegex(@"\bUPDATE\s+(?<table>(?:""[^""]+""|[A-Za-z_][A-Za-z0-9_]*)(?:\.(?:""[^""]+""|[A-Za-z_][A-Za-z0-9_]*))?)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UpdatePattern();

    [GeneratedRegex(@"\bLIMIT\s+(\d+)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LimitPattern();

    [GeneratedRegex(@"@([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant)]
    private static partial Regex ParamPattern();

    [GeneratedRegex(@"\bSELECT\b[\s\S]*\bINTO\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SelectIntoPattern();
}
