using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Morph.Cli.Schema;

public static class MorphYamlSerializer
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults | DefaultValuesHandling.OmitNull)
        .Build();

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static string Serialize<T>(T value) => Serializer.Serialize(value);

    public static T Deserialize<T>(string yaml) => Deserializer.Deserialize<T>(yaml);

    public static DefinitionIr LoadDefinition(string path)
    {
        var yaml = File.ReadAllText(path);
        var ir = Deserialize<DefinitionIr>(yaml);
        if (string.IsNullOrWhiteSpace(ir.Slug))
        {
            throw new InvalidOperationException($"Definition file '{path}' is missing slug.");
        }

        if (string.IsNullOrWhiteSpace(ir.DisplayName))
        {
            ir.DisplayName = ir.Slug;
        }

        foreach (var field in ir.Fields)
        {
            if (string.IsNullOrWhiteSpace(field.DisplayName))
            {
                field.DisplayName = field.Slug;
            }

            field.FieldId ??= Guid.NewGuid();
        }

        foreach (var use in ir.ComponentUses)
        {
            use.ComponentUseId ??= Guid.NewGuid();
        }

        return ir;
    }

    public static void SaveDefinition(string path, DefinitionIr ir)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, Serialize(ir));
    }

    public static PolicyIr LoadPolicy(string path)
    {
        var ir = Deserialize<PolicyIr>(File.ReadAllText(path));
        if (string.IsNullOrWhiteSpace(ir.Name))
        {
            throw new InvalidOperationException($"Policy file '{path}' is missing name.");
        }

        return ir;
    }

    public static void SavePolicy(string path, PolicyIr ir)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, Serialize(ir));
    }

    /// <summary>Stable canonical YAML for diffing (sorted fields/uses).</summary>
    public static string Canonicalize(DefinitionIr ir)
    {
        var clone = new DefinitionIr
        {
            ApiVersion = ir.ApiVersion,
            Kind = ir.Kind,
            Slug = ir.Slug,
            DisplayName = ir.DisplayName,
            Fields = ir.Fields
                .OrderBy(f => f.Slug, StringComparer.Ordinal)
                .Select(f => new FieldIr
                {
                    FieldId = f.FieldId,
                    Slug = f.Slug,
                    DisplayName = f.DisplayName,
                    DataType = f.DataType,
                    IsRequired = f.IsRequired,
                    IsIndexed = f.IsIndexed,
                    IsUnique = f.IsUnique,
                    Encrypt = f.Encrypt
                })
                .ToList(),
            ComponentUses = ir.ComponentUses
                .OrderBy(c => c.Alias, StringComparer.Ordinal)
                .Select(c => new ComponentUseIr
                {
                    ComponentUseId = c.ComponentUseId,
                    Alias = c.Alias,
                    TargetSlug = c.TargetSlug,
                    TargetDefinitionId = c.TargetDefinitionId,
                    PinnedVersionId = c.PinnedVersionId,
                    PinLatest = c.PinLatest,
                    Mode = c.Mode,
                    IsRequired = c.IsRequired
                })
                .ToList(),
            ConflictResolutions = ir.ConflictResolutions
                .OrderBy(c => c.ConflictPath, StringComparer.Ordinal)
                .ToList()
        };

        return Serialize(clone);
    }
}
