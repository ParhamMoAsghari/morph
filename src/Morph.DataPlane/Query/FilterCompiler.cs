using Morph.BuildingBlocks.Results;
using Morph.DataPlane.Mapping;
using Npgsql;
using NpgsqlTypes;

namespace Morph.DataPlane.Query;

/// <summary>
/// Compiles an allowlisted filter AST into parameterized SQL predicates.
/// </summary>
public sealed class FilterCompiler
{
    private const int MaxDepth = 8;
    private const int MaxNodes = 64;

    private readonly FieldColumnResolver _resolver;
    private int _paramIndex;
    private int _nodeCount;

    public FilterCompiler(FieldColumnResolver resolver)
    {
        _resolver = resolver;
    }

    public Result<(string Sql, List<NpgsqlParameter> Parameters)> Compile(FilterNode? filter)
    {
        _paramIndex = 0;
        _nodeCount = 0;
        var parameters = new List<NpgsqlParameter>();

        if (filter is null)
        {
            return Result.Success(("TRUE", parameters));
        }

        try
        {
            var sql = CompileNode(filter, parameters, depth: 0);
            return Result.Success((sql, parameters));
        }
        catch (FilterCompileException ex)
        {
            return Result.Failure<(string, List<NpgsqlParameter>)>(Error.Validation(ex.Message));
        }
    }

    public Result<(string Sql, List<NpgsqlParameter> Parameters)> CompileSort(
        IReadOnlyList<SortSpec>? sorts,
        string defaultOrderSql = "\"created_at\" DESC, \"id\" DESC")
    {
        _paramIndex = 0;
        var parameters = new List<NpgsqlParameter>();
        if (sorts is null || sorts.Count == 0)
        {
            return Result.Success((defaultOrderSql, parameters));
        }

        if (sorts.Count > 5)
        {
            return Result.Failure<(string, List<NpgsqlParameter>)>(
                Error.Validation("At most 5 sort fields are allowed."));
        }

        var parts = new List<string>();
        foreach (var sort in sorts)
        {
            var column = _resolver.Resolve(sort.FieldPath);
            if (column is null)
            {
                return Result.Failure<(string, List<NpgsqlParameter>)>(
                    Error.Validation($"Unknown sort field '{sort.FieldPath}'."));
            }

            var expr = _resolver.ToSqlExpression(column);
            parts.Add(expr + (sort.Descending ? " DESC" : " ASC"));
        }

        parts.Add("\"id\" DESC");
        return Result.Success((string.Join(", ", parts), parameters));
    }

    private string CompileNode(FilterNode node, List<NpgsqlParameter> parameters, int depth)
    {
        if (depth > MaxDepth)
        {
            throw new FilterCompileException("Filter nesting exceeds maximum depth.");
        }

        if (++_nodeCount > MaxNodes)
        {
            throw new FilterCompileException("Filter exceeds maximum node count.");
        }

        return node switch
        {
            FilterAnd and => Combine(and.Children, "AND", parameters, depth),
            FilterOr or => Combine(or.Children, "OR", parameters, depth),
            FilterNot not => $"NOT ({CompileNode(not.Child, parameters, depth + 1)})",
            FilterComparison cmp => CompileComparison(cmp, parameters),
            _ => throw new FilterCompileException($"Unsupported filter node '{node.GetType().Name}'.")
        };
    }

    private string Combine(
        IReadOnlyList<FilterNode> children,
        string op,
        List<NpgsqlParameter> parameters,
        int depth)
    {
        if (children.Count == 0)
        {
            return "TRUE";
        }

        var parts = children.Select(c => $"({CompileNode(c, parameters, depth + 1)})");
        return string.Join($" {op} ", parts);
    }

    private string CompileComparison(FilterComparison cmp, List<NpgsqlParameter> parameters)
    {
        var column = _resolver.Resolve(cmp.FieldPath);
        if (column is null)
        {
            throw new FilterCompileException($"Unknown filter field '{cmp.FieldPath}'.");
        }

        var expr = _resolver.ToSqlExpression(column);

        switch (cmp.Operator)
        {
            case FilterOperator.IsNull:
                return $"{expr} IS NULL";
            case FilterOperator.IsNotNull:
                return $"{expr} IS NOT NULL";
            case FilterOperator.In:
            {
                if (cmp.Value is not System.Collections.IEnumerable enumerable || cmp.Value is string)
                {
                    throw new FilterCompileException("IN operator requires an array value.");
                }

                var names = new List<string>();
                foreach (var item in enumerable)
                {
                    var p = AddParam(parameters, item);
                    names.Add("@" + p.ParameterName);
                }

                if (names.Count == 0)
                {
                    return "FALSE";
                }

                return $"{expr} IN ({string.Join(", ", names)})";
            }
            case FilterOperator.Contains:
            {
                var p = AddParam(parameters, "%" + Convert.ToString(cmp.Value) + "%");
                return $"{expr} ILIKE @{p.ParameterName}";
            }
            case FilterOperator.Eq:
            case FilterOperator.Neq:
            case FilterOperator.Gt:
            case FilterOperator.Gte:
            case FilterOperator.Lt:
            case FilterOperator.Lte:
            {
                var p = AddParam(parameters, cmp.Value);
                var sqlOp = cmp.Operator switch
                {
                    FilterOperator.Eq => "=",
                    FilterOperator.Neq => "<>",
                    FilterOperator.Gt => ">",
                    FilterOperator.Gte => ">=",
                    FilterOperator.Lt => "<",
                    FilterOperator.Lte => "<=",
                    _ => "="
                };
                return $"{expr} {sqlOp} @{p.ParameterName}";
            }
            default:
                throw new FilterCompileException($"Operator '{cmp.Operator}' is not allowlisted.");
        }
    }

    private NpgsqlParameter AddParam(List<NpgsqlParameter> parameters, object? value)
    {
        var name = "p" + _paramIndex++;
        var parameter = value switch
        {
            null => new NpgsqlParameter(name, DBNull.Value),
            Guid g => new NpgsqlParameter(name, NpgsqlDbType.Uuid) { Value = g },
            bool b => new NpgsqlParameter(name, NpgsqlDbType.Boolean) { Value = b },
            int i => new NpgsqlParameter(name, NpgsqlDbType.Integer) { Value = i },
            long l => new NpgsqlParameter(name, NpgsqlDbType.Bigint) { Value = l },
            decimal d => new NpgsqlParameter(name, NpgsqlDbType.Numeric) { Value = d },
            DateTimeOffset dto => new NpgsqlParameter(name, NpgsqlDbType.TimestampTz) { Value = dto },
            DateTime dt => new NpgsqlParameter(name, NpgsqlDbType.TimestampTz) { Value = dt },
            string s when Guid.TryParse(s, out var parsed) =>
                new NpgsqlParameter(name, NpgsqlDbType.Uuid) { Value = parsed },
            string s => new NpgsqlParameter(name, NpgsqlDbType.Text) { Value = s },
            _ => new NpgsqlParameter(name, value)
        };
        parameters.Add(parameter);
        return parameter;
    }
}

public sealed class FilterCompileException : Exception
{
    public FilterCompileException(string message) : base(message)
    {
    }
}
