using System.Text.Json;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Morph.ProcessEngine.Ir;

public static class ProcessIrSerializer
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static ProcessDocument ParseYaml(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var obj = deserializer.Deserialize<object>(yaml)
            ?? throw new InvalidOperationException("Process YAML deserialized to null.");

        // Round-trip via JSON so JsonElement maps stay consistent.
        var json = JsonSerializer.Serialize(ToJsonCompatible(obj), JsonOptions);
        return ParseJson(json);
    }

    public static ProcessDocument ParseJson(string json) =>
        JsonSerializer.Deserialize<ProcessDocument>(json, JsonOptions)
        ?? throw new InvalidOperationException("Process JSON deserialized to null.");

    public static string ToJson(ProcessDocument document) =>
        JsonSerializer.Serialize(document, JsonOptions);

    public static string ToJson(CompiledProcess compiled) =>
        JsonSerializer.Serialize(compiled, JsonOptions);

    public static CompiledProcess ParseCompiled(string json) =>
        JsonSerializer.Deserialize<CompiledProcess>(json, JsonOptions)
        ?? throw new InvalidOperationException("Compiled process JSON deserialized to null.");

    private static object? ToJsonCompatible(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case string or bool or int or long or float or double or decimal:
                return value;
            case Dictionary<object, object> map:
            {
                var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var (k, v) in map)
                {
                    dict[k?.ToString() ?? ""] = ToJsonCompatible(v);
                }

                return dict;
            }
            case List<object> list:
                return list.Select(ToJsonCompatible).ToList();
            default:
                return value.ToString();
        }
    }
}
