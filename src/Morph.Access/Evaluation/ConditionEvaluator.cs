using Morph.Access.Domain;

namespace Morph.Access.Evaluation;

public static class ConditionEvaluator
{
    public static bool Evaluate(ConditionExpression? condition, AccessContext context)
    {
        if (condition is null)
        {
            return true;
        }

        return EvaluateNode(condition, context);
    }

    private static bool EvaluateNode(ConditionExpression node, AccessContext context)
    {
        var op = node.Op.Trim().ToLowerInvariant();
        return op switch
        {
            "and" => (node.Children ?? []).All(c => EvaluateNode(c, context)),
            "or" => (node.Children ?? []).Any(c => EvaluateNode(c, context)),
            "not" => !(node.Children?.FirstOrDefault() is { } child && EvaluateNode(child, context)),
            "eq" => EqualsValues(Resolve(node.Left, context), Resolve(node.Right, context)),
            "neq" => !EqualsValues(Resolve(node.Left, context), Resolve(node.Right, context)),
            "gt" => Compare(Resolve(node.Left, context), Resolve(node.Right, context)) > 0,
            "gte" => Compare(Resolve(node.Left, context), Resolve(node.Right, context)) >= 0,
            "lt" => Compare(Resolve(node.Left, context), Resolve(node.Right, context)) < 0,
            "lte" => Compare(Resolve(node.Left, context), Resolve(node.Right, context)) <= 0,
            "in" => IsIn(Resolve(node.Left, context), Resolve(node.Right, context)),
            _ => false
        };
    }

    public static object? Resolve(ConditionOperand? operand, AccessContext context)
    {
        if (operand is null)
        {
            return null;
        }

        if (!operand.IsAttribute)
        {
            return operand.LiteralValue;
        }

        return ResolvePath(operand.AttributePath, context);
    }

    public static object? ResolvePath(string? path, AccessContext context)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var parts = path.Split('.', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        var root = parts[0].ToLowerInvariant();
        var rest = parts.Length > 1 ? parts[1] : null;

        return root switch
        {
            "subject" => ResolveSubject(rest, context.Subject),
            "resource" => ResolveResource(rest, context.Resource),
            "env" or "environment" => ResolveDict(rest, context.Environment),
            "action" => rest is null ? context.Action : null,
            _ => null
        };
    }

    private static object? ResolveSubject(string? path, AccessSubject subject)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return subject.PrincipalId;
        }

        return path.ToLowerInvariant() switch
        {
            "sub" or "id" or "principal_id" or "principalid" => subject.PrincipalId,
            "roles" => subject.Roles,
            _ => ResolveDict(path, subject.Attributes)
        };
    }

    private static object? ResolveResource(string? path, AccessResource resource)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return resource.Type;
        }

        return path.ToLowerInvariant() switch
        {
            "type" or "entity" or "slug" => resource.Type,
            "field" or "field_path" or "fieldpath" => resource.FieldPath,
            "owner_id" or "ownerid" => ResolveDict("owner_id", resource.Attributes)
                                       ?? ResolveDict("created_by", resource.Attributes),
            "created_by" or "createdby" => ResolveDict("created_by", resource.Attributes)
                                           ?? ResolveDict("owner_id", resource.Attributes),
            _ => ResolveDict(path, resource.Attributes)
        };
    }

    private static object? ResolveDict(string? key, IReadOnlyDictionary<string, object?> dict)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return dict.TryGetValue(key, out var value) ? value : null;
    }

    private static bool EqualsValues(object? left, object? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        if (left is string ls && right is string rs)
        {
            return string.Equals(ls, rs, StringComparison.OrdinalIgnoreCase);
        }

        if (TryToString(left) is { } lstr && TryToString(right) is { } rstr
            && Guid.TryParse(lstr, out var lg) && Guid.TryParse(rstr, out var rg))
        {
            return lg == rg;
        }

        return Equals(left, right) || string.Equals(Convert.ToString(left), Convert.ToString(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryToString(object value) => Convert.ToString(value);

    private static int Compare(object? left, object? right)
    {
        if (left is IComparable lc && right is not null)
        {
            try
            {
                return lc.CompareTo(Convert.ChangeType(right, left.GetType()));
            }
            catch
            {
                // fall through
            }
        }

        return string.Compare(Convert.ToString(left), Convert.ToString(right), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIn(object? left, object? right)
    {
        if (right is System.Collections.IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                if (EqualsValues(left, item))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
