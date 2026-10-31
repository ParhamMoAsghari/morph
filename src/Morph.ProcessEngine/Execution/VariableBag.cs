using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Morph.ProcessEngine.Execution;

public sealed class VariableBag
{
    private static readonly Regex TokenRegex = new(@"\{\{\s*([^}]+?)\s*\}\}", RegexOptions.Compiled);

    private readonly Dictionary<string, object?> _vars;

    public VariableBag(IDictionary<string, object?>? seed = null)
    {
        _vars = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (seed is not null)
        {
            foreach (var (k, v) in seed)
            {
                _vars[k] = v;
            }
        }
    }

    public IReadOnlyDictionary<string, object?> Snapshot =>
        new Dictionary<string, object?>(_vars, StringComparer.OrdinalIgnoreCase);

    public void Set(string name, object? value) => _vars[name] = value;

    public bool TryGet(string name, out object? value) => _vars.TryGetValue(name, out value);

    public object? GetPath(string path)
    {
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        if (!_vars.TryGetValue(parts[0], out var current))
        {
            return null;
        }

        for (var i = 1; i < parts.Length; i++)
        {
            current = Descend(current, parts[i]);
            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    public string Interpolate(string template)
    {
        return TokenRegex.Replace(template, match =>
        {
            var path = match.Groups[1].Value.Trim();
            var value = GetPath(path);
            return Format(value);
        });
    }

    public object? Resolve(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
            {
                var s = element.GetString() ?? "";
                if (s.StartsWith("{{", StringComparison.Ordinal) && s.EndsWith("}}", StringComparison.Ordinal)
                    && s.Count(c => c == '{') == 2)
                {
                    var path = s[2..^2].Trim();
                    return GetPath(path);
                }

                return TokenRegex.IsMatch(s) ? Interpolate(s) : s;
            }
            case JsonValueKind.Number:
                if (element.TryGetInt64(out var l))
                {
                    return l;
                }

                return element.GetDouble();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            case JsonValueKind.Object:
            {
                var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in element.EnumerateObject())
                {
                    dict[prop.Name] = Resolve(prop.Value);
                }

                return dict;
            }
            case JsonValueKind.Array:
            {
                var list = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(Resolve(item));
                }

                return list;
            }
            default:
                return null;
        }
    }

    public bool EvaluateCondition(string expression)
    {
        expression = expression.Trim();
        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        // Support: {{path}}, {{path}} == value, {{path}} != value, true/false literals
        foreach (var op in new[] { "==", "!=" })
        {
            var idx = expression.IndexOf(op, StringComparison.Ordinal);
            if (idx < 0)
            {
                continue;
            }

            var leftRaw = expression[..idx].Trim();
            var rightRaw = expression[(idx + op.Length)..].Trim();
            var left = ResolveOperand(leftRaw);
            var right = ResolveOperand(rightRaw);
            var equal = ValuesEqual(left, right);
            return op == "==" ? equal : !equal;
        }

        var single = ResolveOperand(expression);
        return IsTruthy(single);
    }

    public string ToJson() =>
        JsonSerializer.Serialize(ToJsonCompatible(_vars));

    public static VariableBag FromJson(string json)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        var bag = new VariableBag();
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            bag.Set(prop.Name, FromElement(prop.Value));
        }

        return bag;
    }

    private object? ResolveOperand(string raw)
    {
        if (raw.StartsWith("{{", StringComparison.Ordinal) && raw.EndsWith("}}", StringComparison.Ordinal))
        {
            return GetPath(raw[2..^2].Trim());
        }

        if (bool.TryParse(raw, out var b))
        {
            return b;
        }

        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
        {
            return l;
        }

        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
        {
            return d;
        }

        if ((raw.StartsWith('"') && raw.EndsWith('"')) || (raw.StartsWith('\'') && raw.EndsWith('\'')))
        {
            return raw[1..^1];
        }

        return Interpolate(raw);
    }

    private static object? Descend(object? current, string key)
    {
        if (current is IDictionary<string, object?> dict)
        {
            return dict.TryGetValue(key, out var v) ? v : null;
        }

        if (current is IDictionary plain)
        {
            foreach (DictionaryEntry entry in plain)
            {
                if (string.Equals(entry.Key?.ToString(), key, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Value;
                }
            }

            return null;
        }

        if (current is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Object && je.TryGetProperty(key, out var prop))
            {
                return FromElement(prop);
            }

            return null;
        }

        return null;
    }

    private static object? FromElement(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => FromElement(p.Value), StringComparer.OrdinalIgnoreCase),
            JsonValueKind.Array => element.EnumerateArray().Select(FromElement).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };

    private static object? ToJsonCompatible(object? value) =>
        value switch
        {
            null => null,
            string or bool or byte or short or int or long or float or double or decimal => value,
            JsonElement je => FromElement(je),
            IDictionary<string, object?> dict => dict.ToDictionary(
                kv => kv.Key,
                kv => ToJsonCompatible(kv.Value),
                StringComparer.OrdinalIgnoreCase),
            IEnumerable enumerable when value is not string => enumerable.Cast<object?>().Select(ToJsonCompatible).ToList(),
            _ => value.ToString()
        };

    private static string Format(object? value) =>
        value switch
        {
            null => "",
            string s => s,
            bool b => b ? "true" : "false",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture) ?? "",
            _ => value.ToString() ?? ""
        };

    private static bool IsTruthy(object? value) =>
        value switch
        {
            null => false,
            bool b => b,
            string s => !string.IsNullOrWhiteSpace(s) && !string.Equals(s, "false", StringComparison.OrdinalIgnoreCase),
            IConvertible c => Convert.ToDouble(c, CultureInfo.InvariantCulture) != 0,
            _ => true
        };

    private static bool ValuesEqual(object? left, object? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        if (left is IConvertible && right is IConvertible)
        {
            try
            {
                var ld = Convert.ToDouble(left, CultureInfo.InvariantCulture);
                var rd = Convert.ToDouble(right, CultureInfo.InvariantCulture);
                return Math.Abs(ld - rd) < 0.0000001;
            }
            catch
            {
                // fall through
            }
        }

        return string.Equals(Format(left), Format(right), StringComparison.OrdinalIgnoreCase);
    }
}
