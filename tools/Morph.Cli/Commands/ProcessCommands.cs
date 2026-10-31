using System.Text;
using System.Text.Json;
using Morph.Cli.Config;
using Morph.Client.Models;

namespace Morph.Cli.Commands;

internal static class ProcessCommands
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintHelp();
            return args.Length == 0 ? 1 : 0;
        }

        var rest = args.Skip(1).ToArray();
        return args[0].ToLowerInvariant() switch
        {
            "push" => await PushAsync(rest),
            "diff" => await DiffAsync(rest),
            "publish" => await PublishAsync(rest),
            "run" => await RunProcessAsync(rest),
            "logs" => await LogsAsync(rest),
            _ => Unknown(args[0])
        };
    }

    private static async Task<int> PushAsync(string[] args)
    {
        var slugFilter = ArgUtil.GetOption(args, "--slug");
        if (!TryLink(out var root, out var envId, out var client, out var error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        using (client)
        {
            var files = EnumerateProcessFiles(root)
                .Where(f => slugFilter is null
                            || string.Equals(Path.GetFileNameWithoutExtension(f).Split('.')[0], slugFilter,
                                StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (files.Count == 0)
            {
                Console.WriteLine("No process YAML files under morph/processes.");
                return 0;
            }

            foreach (var file in files)
            {
                var yaml = await File.ReadAllTextAsync(file);
                var response = await client.Processes.PushAsync(envId, new ProcessUpsertRequest { Yaml = yaml });
                Console.WriteLine($"push  {response.Slug} → {response.DefinitionId} v={response.VersionId} ({response.Status})");
            }

            return 0;
        }
    }

    private static async Task<int> DiffAsync(string[] args)
    {
        var slugFilter = ArgUtil.GetOption(args, "--slug");
        if (!TryLink(out var root, out var envId, out var client, out var error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        using (client)
        {
            var remote = await client.Processes.ListAsync(envId);
            var remoteBySlug = remote.ToDictionary(r => r.Slug, StringComparer.OrdinalIgnoreCase);
            var localFiles = EnumerateProcessFiles(root).ToList();
            var exit = 0;

            foreach (var file in localFiles)
            {
                var yaml = await File.ReadAllTextAsync(file);
                var localSlug = GuessSlug(yaml, file);
                if (slugFilter is not null
                    && !string.Equals(localSlug, slugFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!remoteBySlug.TryGetValue(localSlug, out var def) || def.CurrentPublishedVersionId is null)
                {
                    Console.WriteLine($"+ {localSlug} (local only / unpublished remote)");
                    exit = 1;
                    continue;
                }

                var authoring = await client.Processes.GetAuthoringAsync(envId, def.CurrentPublishedVersionId.Value);
                var remoteJson = JsonSerializer.Serialize(authoring);
                var localNormalized = NormalizeYaml(yaml);
                if (!string.Equals(localNormalized, NormalizeYaml(remoteJson), StringComparison.Ordinal))
                {
                    Console.WriteLine($"~ {localSlug} differs from published remote");
                    exit = 1;
                }
                else
                {
                    Console.WriteLine($"= {localSlug}");
                }
            }

            foreach (var remoteDef in remoteBySlug.Values)
            {
                if (localFiles.Any(f =>
                        string.Equals(GuessSlug(File.ReadAllText(f), f), remoteDef.Slug,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (slugFilter is not null
                    && !string.Equals(remoteDef.Slug, slugFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Console.WriteLine($"- {remoteDef.Slug} (remote only)");
                exit = 1;
            }

            return ArgUtil.HasFlag(args, "--exit-code") ? exit : 0;
        }
    }

    private static async Task<int> PublishAsync(string[] args)
    {
        var slug = ArgUtil.GetOption(args, "--slug");
        var versionRaw = ArgUtil.GetOption(args, "--version");
        if (!TryLink(out _, out var envId, out var client, out var error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        using (client)
        {
            Guid versionId;
            if (!string.IsNullOrWhiteSpace(versionRaw) && Guid.TryParse(versionRaw, out versionId))
            {
                // ok
            }
            else if (!string.IsNullOrWhiteSpace(slug))
            {
                var pushed = await client.Processes.PushAsync(
                    envId,
                    new ProcessUpsertRequest
                    {
                        Yaml = await FindLocalYamlAsync(ProjectPaths.FindRoot(), slug!)
                    });
                versionId = pushed.VersionId;
            }
            else
            {
                Console.Error.WriteLine("Provide --slug or --version.");
                return 1;
            }

            var published = await client.Processes.PublishAsync(envId, versionId);
            Console.WriteLine($"published {published.Slug} v{published.VersionNumber} ({published.VersionId})");
            return 0;
        }
    }

    private static async Task<int> RunProcessAsync(string[] args)
    {
        var slug = ArgUtil.GetOption(args, "--slug") ?? (args.Length > 0 && !args[0].StartsWith('-') ? args[0] : null);
        var inputJson = ArgUtil.GetOption(args, "--input");
        if (string.IsNullOrWhiteSpace(slug))
        {
            Console.Error.WriteLine("Usage: morph process run --slug <slug> [--input '{...}']");
            return 1;
        }

        if (!TryLink(out _, out var envId, out var client, out var error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        using (client)
        {
            Dictionary<string, object?>? input = null;
            if (!string.IsNullOrWhiteSpace(inputJson))
            {
                input = JsonSerializer.Deserialize<Dictionary<string, object?>>(inputJson);
            }

            var instance = await client.Processes.RunAsync(envId, slug, new ProcessRunRequest { Input = input });
            Console.WriteLine($"run {instance.Slug} → {instance.InstanceId} ({instance.Status})");
            if (!string.IsNullOrWhiteSpace(instance.FailureMessage))
            {
                Console.Error.WriteLine(instance.FailureMessage);
                return 1;
            }

            return 0;
        }
    }

    private static async Task<int> LogsAsync(string[] args)
    {
        var instanceRaw = ArgUtil.GetOption(args, "--instance")
                          ?? (args.Length > 0 && !args[0].StartsWith('-') ? args[0] : null);
        if (string.IsNullOrWhiteSpace(instanceRaw) || !Guid.TryParse(instanceRaw, out var instanceId))
        {
            Console.Error.WriteLine("Usage: morph process logs --instance <guid>");
            return 1;
        }

        if (!TryLink(out _, out var envId, out var client, out var error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        using (client)
        {
            var logs = await client.Processes.LogsAsync(envId, instanceId);
            foreach (var log in logs)
            {
                Console.WriteLine($"{log.StartedAt:O} {log.StepId} {log.ActivityType} {log.Status}");
                if (!string.IsNullOrWhiteSpace(log.ErrorMessage))
                {
                    Console.WriteLine($"  error: {log.ErrorMessage}");
                }
            }

            return 0;
        }
    }

    private static bool TryLink(
        out string root,
        out Guid envId,
        out Morph.Client.MorphClient client,
        out string error)
    {
        root = ProjectPaths.FindRoot();
        var config = ProjectConfigStore.Load(root);
        try
        {
            (_, envId) = CliClientFactory.RequireLink(root, config);
            client = CliClientFactory.Create(root, config);
            error = "";
            return true;
        }
        catch (Exception ex)
        {
            envId = default;
            client = null!;
            error = ex.Message;
            return false;
        }
    }

    private static IEnumerable<string> EnumerateProcessFiles(string root)
    {
        var dir = Path.Combine(root, "morph", "processes");
        if (!Directory.Exists(dir))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(dir, "*.yaml", SearchOption.AllDirectories)
                     .Concat(Directory.EnumerateFiles(dir, "*.yml", SearchOption.AllDirectories)))
        {
            yield return file;
        }
    }

    private static async Task<string> FindLocalYamlAsync(string root, string slug)
    {
        foreach (var file in EnumerateProcessFiles(root))
        {
            var yaml = await File.ReadAllTextAsync(file);
            if (string.Equals(GuessSlug(yaml, file), slug, StringComparison.OrdinalIgnoreCase))
            {
                return yaml;
            }
        }

        throw new InvalidOperationException($"No local process YAML found for slug '{slug}'.");
    }

    private static string GuessSlug(string yaml, string path)
    {
        foreach (var line in yaml.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("slug:", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed["slug:".Length..].Trim().Trim('"', '\'');
            }
        }

        return Path.GetFileNameWithoutExtension(path).Split('.')[0];
    }

    private static string NormalizeYaml(string text)
    {
        var sb = new StringBuilder();
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            var t = line.TrimEnd();
            if (t.Length > 0)
            {
                sb.AppendLine(t);
            }
        }

        return sb.ToString();
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown process command '{command}'.");
        PrintHelp();
        return 1;
    }

    private static bool IsHelp(string value) => value is "-h" or "--help" or "help";

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Usage:
              morph process push [--slug <slug>]
              morph process diff [--slug <slug>] [--exit-code]
              morph process publish --slug <slug> | --version <guid>
              morph process run --slug <slug> [--input '{...}']
              morph process logs --instance <guid>
            """);
    }
}
