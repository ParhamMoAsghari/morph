using Morph.Cli.Schema;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Morph.Cli.Config;

public sealed class ProjectConfig
{
    public string Name { get; set; } = "morph-app";

    public string Version { get; set; } = "0.1.0";

    public string ApiUrl { get; set; } = "http://localhost:5100";

    public Guid? ProjectId { get; set; }

    public Guid? EnvironmentId { get; set; }

    public Guid? OrganizationId { get; set; }

    public CodegenConfig Codegen { get; set; } = new();

    public List<PluginRefConfig>? Plugins { get; set; }
}

public sealed class CodegenConfig
{
    public string Typescript { get; set; } = "src/generated";

    public string Csharp { get; set; } = "Generated";
}

public sealed class PluginRefConfig
{
    public string Id { get; set; } = "";

    public string? Version { get; set; }

    public string? ConfigRef { get; set; }
}

public sealed class LinkState
{
    public Guid ProjectId { get; set; }

    public Guid EnvironmentId { get; set; }

    public Guid? OrganizationId { get; set; }

    public string ApiUrl { get; set; } = "http://localhost:5100";

    public DateTimeOffset LinkedAt { get; set; }
}

public sealed class CredentialStore
{
    public string? AccessToken { get; set; }

    public string? RefreshToken { get; set; }

    public string? Email { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }
}

public static class ProjectPaths
{
    public const string MorphYaml = "morph.yaml";
    public const string MorphDir = "morph";
    public const string DefinitionsDir = "morph/definitions";
    public const string PoliciesDir = "morph/policies";
    public const string DotMorphDir = ".morph";
    public const string LinkFile = ".morph/link.json";
    public const string CredentialsFile = ".morph/credentials.json";

    public static string FindRoot(string? start = null)
    {
        var dir = new DirectoryInfo(Path.GetFullPath(start ?? Directory.GetCurrentDirectory()));
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, MorphYaml)))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Path.GetFullPath(start ?? Directory.GetCurrentDirectory());
    }
}

public static class ProjectConfigStore
{
    private static readonly ISerializer YamlSerializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static ProjectConfig Load(string root)
    {
        var path = Path.Combine(root, ProjectPaths.MorphYaml);
        if (!File.Exists(path))
        {
            return new ProjectConfig();
        }

        return YamlDeserializer.Deserialize<ProjectConfig>(File.ReadAllText(path)) ?? new ProjectConfig();
    }

    public static void Save(string root, ProjectConfig config)
    {
        File.WriteAllText(Path.Combine(root, ProjectPaths.MorphYaml), YamlSerializer.Serialize(config));
    }

    public static LinkState? LoadLink(string root)
    {
        var path = Path.Combine(root, ProjectPaths.LinkFile);
        if (!File.Exists(path))
        {
            return null;
        }

        return System.Text.Json.JsonSerializer.Deserialize<LinkState>(File.ReadAllText(path));
    }

    public static void SaveLink(string root, LinkState link)
    {
        Directory.CreateDirectory(Path.Combine(root, ProjectPaths.DotMorphDir));
        File.WriteAllText(
            Path.Combine(root, ProjectPaths.LinkFile),
            System.Text.Json.JsonSerializer.Serialize(link, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    public static CredentialStore? LoadCredentials(string root)
    {
        var path = Path.Combine(root, ProjectPaths.CredentialsFile);
        if (!File.Exists(path))
        {
            return null;
        }

        return System.Text.Json.JsonSerializer.Deserialize<CredentialStore>(File.ReadAllText(path));
    }

    public static void SaveCredentials(string root, CredentialStore store)
    {
        Directory.CreateDirectory(Path.Combine(root, ProjectPaths.DotMorphDir));
        File.WriteAllText(
            Path.Combine(root, ProjectPaths.CredentialsFile),
            System.Text.Json.JsonSerializer.Serialize(store, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    public static IReadOnlyList<string> EnumerateDefinitionFiles(string root)
    {
        var dir = Path.Combine(root, ProjectPaths.DefinitionsDir);
        if (!Directory.Exists(dir))
        {
            return [];
        }

        return Directory.EnumerateFiles(dir, "*.yaml")
            .Concat(Directory.EnumerateFiles(dir, "*.yml"))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<string> EnumeratePolicyFiles(string root)
    {
        var dir = Path.Combine(root, ProjectPaths.PoliciesDir);
        if (!Directory.Exists(dir))
        {
            return [];
        }

        return Directory.EnumerateFiles(dir, "*.yaml")
            .Concat(Directory.EnumerateFiles(dir, "*.yml"))
            .Where(f => !Path.GetFileName(f).Equals("roles.yaml", StringComparison.OrdinalIgnoreCase)
                        && !Path.GetFileName(f).Equals("roles.yml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<DefinitionIr> LoadAllDefinitions(string root) =>
        EnumerateDefinitionFiles(root).Select(MorphYamlSerializer.LoadDefinition).ToList();
}
