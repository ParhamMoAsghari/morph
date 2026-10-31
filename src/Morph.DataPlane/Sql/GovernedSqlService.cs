using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Morph.Access.Domain;
using Morph.Access.Evaluation;
using Morph.BuildingBlocks.Results;
using Morph.DataPlane.Cache;
using Morph.DataPlane.Mapping;
using Morph.DataPlane.Query;
using Morph.DataPlane.Services;
using Npgsql;

namespace Morph.DataPlane.Sql;

public sealed partial class GovernedSqlService
{
    private readonly IPublishedModelCache _cache;
    private readonly IAccessEvaluator _access;
    private readonly NpgsqlDataSource _dataSource;
    private readonly GovernedSqlOptions _options;

    public GovernedSqlService(
        IPublishedModelCache cache,
        IAccessEvaluator access,
        NpgsqlDataSource dataSource,
        GovernedSqlOptions? options = null)
    {
        _cache = cache;
        _access = access;
        _dataSource = dataSource;
        _options = options ?? new GovernedSqlOptions();
    }

    public async Task<Result<GovernedSqlResult>> ExecuteAsync(
        Guid environmentId,
        string sql,
        IReadOnlyDictionary<string, object?>? parameters,
        AccessSubject subject,
        CancellationToken cancellationToken = default)
    {
        var parsed = GovernedSqlParser.Parse(sql, _options.MaxSqlLength);
        if (parsed.IsFailure)
        {
            return Result.Failure<GovernedSqlResult>(parsed.Error!);
        }

        var statement = parsed.Value;
        var models = new Dictionary<string, PublishedEntityModel>(StringComparer.OrdinalIgnoreCase);

        foreach (var slug in statement.EntitySlugs)
        {
            var model = await _cache.GetBySlugAsync(environmentId, slug, cancellationToken).ConfigureAwait(false);
            if (model is null)
            {
                return Result.Failure<GovernedSqlResult>(
                    Error.Validation($"Entity '{slug}' is not published in this environment."));
            }

            models[slug] = model;
        }

        var primary = models[statement.PrimaryEntitySlug];

        var sqlDecision = await EvaluateAsync(
                primary,
                AccessActions.SqlExecute,
                subject,
                cancellationToken)
            .ConfigureAwait(false);
        if (sqlDecision.IsDenied)
        {
            return Result.Failure<GovernedSqlResult>(Error.Forbidden(sqlDecision.Reason));
        }

        var dataAction = statement.Kind switch
        {
            GovernedSqlKind.Select => AccessActions.List,
            GovernedSqlKind.Insert => AccessActions.Create,
            GovernedSqlKind.Update => AccessActions.Update,
            GovernedSqlKind.Delete => AccessActions.Delete,
            _ => AccessActions.List
        };

        AccessDecision? primaryDataDecision = null;
        foreach (var (slug, model) in models)
        {
            var decision = await EvaluateAsync(model, dataAction, subject, cancellationToken)
                .ConfigureAwait(false);
            if (decision.IsDenied)
            {
                return Result.Failure<GovernedSqlResult>(Error.Forbidden(decision.Reason));
            }

            if (slug.Equals(statement.PrimaryEntitySlug, StringComparison.OrdinalIgnoreCase))
            {
                primaryDataDecision = decision;
            }
        }

        var obligations = primaryDataDecision?.Obligations ?? AccessObligations.Empty;
        var obligationFilter = DataPlaneService.ToObligationFilter(obligations, subject);

        string rewritten;
        bool truncatedLimit;
        try
        {
            rewritten = RewriteSql(statement, models, primary, obligationFilter, out truncatedLimit);
        }
        catch (Exception ex)
        {
            return Result.Failure<GovernedSqlResult>(Error.Validation(ex.Message));
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using (var searchPath = new NpgsqlCommand(
                             $"SET LOCAL search_path TO {QuoteIdent(primary.SchemaName)}, public",
                             connection,
                             tx))
            {
                await searchPath.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var timeout = new NpgsqlCommand(
                             $"SET LOCAL statement_timeout = {_options.StatementTimeoutMs}",
                             connection,
                             tx))
            {
                await timeout.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using var cmd = new NpgsqlCommand(rewritten, connection, tx);
            try
            {
                BindParameters(cmd, statement, parameters);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Result.Failure<GovernedSqlResult>(Error.Validation(ex.Message));
            }

            if (statement.Kind == GovernedSqlKind.Select)
            {
                var rows = new List<IReadOnlyDictionary<string, object?>>();
                await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            var name = reader.GetName(i);
                            var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                            if (value is DateTime dt)
                            {
                                value = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                            }

                            row[name] = value;
                        }

                        if (obligations.DeniedReadFields.Count > 0)
                        {
                            foreach (var denied in obligations.DeniedReadFields)
                            {
                                row.Remove(denied);
                            }
                        }

                        rows.Add(row);
                    }
                }

                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
                return Result.Success(new GovernedSqlResult
                {
                    Kind = statement.Kind,
                    PrimaryEntity = statement.PrimaryEntitySlug,
                    Rows = rows,
                    RowCount = rows.Count,
                    Truncated = truncatedLimit
                });
            }

            var affected = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(new GovernedSqlResult
            {
                Kind = statement.Kind,
                PrimaryEntity = statement.PrimaryEntitySlug,
                Rows = [],
                RowCount = affected,
                Truncated = false
            });
        }
        catch (PostgresException ex)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<GovernedSqlResult>(
                Error.Validation($"Governed SQL execution failed: {ex.MessageText}"));
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure<GovernedSqlResult>(
                Error.Validation($"Governed SQL execution failed: {ex.Message}"));
        }
    }

    private string RewriteSql(
        GovernedSqlStatement statement,
        IReadOnlyDictionary<string, PublishedEntityModel> models,
        PublishedEntityModel primary,
        FilterNode? obligationFilter,
        out bool truncatedLimit)
    {
        truncatedLimit = false;
        var sql = statement.TemplateSql;

        foreach (var (slug, model) in models)
        {
            sql = sql.Replace(
                $"{{{{entity:{slug}}}}}",
                model.StorageMapping.QualifiedTableName,
                StringComparison.OrdinalIgnoreCase);
        }

        if (sql.Contains("{{entity:", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Unresolved entity table placeholder in governed SQL.");
        }

        // Map EffectiveModel field slugs/paths → physical columns / JSON expressions.
        sql = RewriteFieldIdentifiers(sql, primary);

        if (obligationFilter is not null
            && statement.Kind is GovernedSqlKind.Select or GovernedSqlKind.Update or GovernedSqlKind.Delete)
        {
            sql = InjectObligationPredicate(sql, obligationFilter, primary);
        }

        if (statement.Kind == GovernedSqlKind.Select)
        {
            var max = _options.MaxRows;
            var effectiveLimit = statement.ClientLimit is int lim ? Math.Min(lim, max) : max;
            if (statement.ClientLimit is int client && client > max)
            {
                truncatedLimit = true;
            }

            if (statement.HasLimit)
            {
                sql = LimitReplacePattern().Replace(
                    sql,
                    $"LIMIT {effectiveLimit.ToString(CultureInfo.InvariantCulture)}");
            }
            else
            {
                sql = $"{sql.TrimEnd()} LIMIT {effectiveLimit.ToString(CultureInfo.InvariantCulture)}";
            }
        }

        return sql;
    }

    /// <summary>
    /// Rewrites unquoted field slug/path identifiers to storage SQL expressions.
    /// Skips string/quoted identifiers and <c>@params</c>.
    /// </summary>
    private static string RewriteFieldIdentifiers(string sql, PublishedEntityModel model)
    {
        var resolver = new FieldColumnResolver(model.StorageMapping, model.EffectiveModel);
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void Consider(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || replacements.ContainsKey(name))
            {
                return;
            }

            // System columns already match physical names.
            if (name is "id" or "created_at" or "updated_at" or "created_by" or "updated_by"
                or "definition_version_id" or "row_version" or "owner_id")
            {
                return;
            }

            var resolved = resolver.Resolve(name);
            if (resolved is null)
            {
                return;
            }

            var expr = resolver.ToSqlExpression(resolved);
            // Only rewrite when the identifier is not already the physical column token.
            if (!string.Equals(expr, FieldColumnResolver.QuoteIdent(name), StringComparison.OrdinalIgnoreCase)
                && !string.Equals(resolved.ColumnName, name, StringComparison.OrdinalIgnoreCase))
            {
                replacements[name] = expr;
            }
            else if (!string.Equals(resolved.ColumnName, name, StringComparison.OrdinalIgnoreCase))
            {
                replacements[name] = expr;
            }
        }

        foreach (var field in model.EffectiveModel.Fields)
        {
            Consider(field.Slug);
            Consider(field.Path);
        }

        if (replacements.Count == 0)
        {
            return sql;
        }

        var result = sql;
        foreach (var (name, expr) in replacements.OrderByDescending(kv => kv.Key.Length))
        {
            // Do not rewrite inside single-quoted strings or after @ (parameters).
            result = Regex.Replace(
                result,
                $@"(?<![@A-Za-z0-9_""]){Regex.Escape(name)}(?![A-Za-z0-9_""])",
                match => IsInsideSingleQuotes(result, match.Index) ? match.Value : expr,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return result;
    }

    private static bool IsInsideSingleQuotes(string sql, int index)
    {
        var inSingle = false;
        for (var i = 0; i < index; i++)
        {
            if (sql[i] != '\'')
            {
                continue;
            }

            if (inSingle && i + 1 < sql.Length && sql[i + 1] == '\'')
            {
                i++;
                continue;
            }

            inSingle = !inSingle;
        }

        return inSingle;
    }

    private static string InjectObligationPredicate(
        string sql,
        FilterNode obligationFilter,
        PublishedEntityModel model)
    {
        var resolver = new FieldColumnResolver(model.StorageMapping, model.EffectiveModel);
        var compiler = new FilterCompiler(resolver);
        var compiled = compiler.Compile(obligationFilter);
        if (compiled.IsFailure)
        {
            throw new InvalidOperationException(compiled.Error!.Message);
        }

        var predicate = compiled.Value.Sql;
        var i = 0;
        foreach (var p in compiled.Value.Parameters)
        {
            var oldName = p.ParameterName.StartsWith('@') ? p.ParameterName : "@" + p.ParameterName;
            var newName = "@morph_obl_" + i.ToString(CultureInfo.InvariantCulture);
            predicate = predicate.Replace(oldName, newName, StringComparison.OrdinalIgnoreCase);
            predicate = InlineLiteral(predicate, newName, p.Value);
            i++;
        }

        if (WherePattern().IsMatch(sql))
        {
            return WherePattern().Replace(sql, m => $"{m.Value} ({predicate}) AND ", 1);
        }

        var insertAt = FindClauseInsertIndex(sql);
        return sql.Insert(insertAt, $" WHERE ({predicate}) ");
    }

    private static string InlineLiteral(string sql, string paramName, object? value)
    {
        var literal = value switch
        {
            null => "NULL",
            Guid g => $"'{g:D}'::uuid",
            string s => "'" + s.Replace("'", "''", StringComparison.Ordinal) + "'",
            bool b => b ? "TRUE" : "FALSE",
            IFormattable f => Convert.ToString(f, CultureInfo.InvariantCulture) ?? "NULL",
            _ => "'" + (Convert.ToString(value, CultureInfo.InvariantCulture) ?? "")
                .Replace("'", "''", StringComparison.Ordinal) + "'"
        };

        return Regex.Replace(
            sql,
            $@"(?<![A-Za-z0-9_]){Regex.Escape(paramName)}(?![A-Za-z0-9_])",
            literal);
    }

    private static int FindClauseInsertIndex(string sql)
    {
        var upper = sql.ToUpperInvariant();
        var candidates = new[]
        {
            upper.IndexOf(" ORDER BY ", StringComparison.Ordinal),
            upper.IndexOf(" GROUP BY ", StringComparison.Ordinal),
            upper.IndexOf(" LIMIT ", StringComparison.Ordinal),
            upper.IndexOf(" RETURNING ", StringComparison.Ordinal),
            upper.IndexOf(" OFFSET ", StringComparison.Ordinal)
        };

        var hit = candidates.Where(i => i >= 0).DefaultIfEmpty(-1).Min();
        return hit >= 0 ? hit : sql.Length;
    }

    private static void BindParameters(
        NpgsqlCommand cmd,
        GovernedSqlStatement statement,
        IReadOnlyDictionary<string, object?>? parameters)
    {
        parameters ??= new Dictionary<string, object?>();
        foreach (var name in statement.ParameterNames)
        {
            object? value = null;
            var found = false;
            foreach (var (k, v) in parameters)
            {
                if (k.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    value = v;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                throw new InvalidOperationException($"Missing SQL parameter '@{name}'.");
            }

            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
    }

    private async Task<AccessDecision> EvaluateAsync(
        PublishedEntityModel model,
        string action,
        AccessSubject subject,
        CancellationToken cancellationToken)
    {
        var context = new AccessContext
        {
            Subject = subject,
            Action = action,
            Resource = new AccessResource
            {
                Type = model.Slug,
                ProjectId = model.ProjectId,
                EnvironmentId = model.EnvironmentId,
                DefinitionId = model.DefinitionId,
                Attributes = new Dictionary<string, object?>()
            }
        };

        return await _access.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
    }

    private static string QuoteIdent(string ident) =>
        "\"" + ident.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    [GeneratedRegex(@"\bWHERE\b\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WherePattern();

    [GeneratedRegex(@"\bLIMIT\s+\d+\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LimitReplacePattern();
}
