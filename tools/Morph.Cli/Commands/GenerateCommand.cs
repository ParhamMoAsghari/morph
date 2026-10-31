using Morph.Cli.Codegen;
using Morph.Cli.Config;
using Morph.Client.Models;

namespace Morph.Cli.Commands;

internal static class GenerateCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var root = ProjectPaths.FindRoot();
        var config = ProjectConfigStore.Load(root);
        var check = ArgUtil.HasFlag(args, "--check");
        var lang = (ArgUtil.GetOption(args, "--lang") ?? "all").ToLowerInvariant();
        var outTs = ArgUtil.GetOption(args, "--ts-out") ?? Path.Combine(root, config.Codegen.Typescript);
        var outCs = ArgUtil.GetOption(args, "--cs-out") ?? Path.Combine(root, config.Codegen.Csharp);

        Guid envId;
        try
        {
            (_, envId) = CliClientFactory.RequireLink(root, config);
        }
        catch (Exception ex)
        {
            // Offline fallback: generate empty stub notice
            if (ArgUtil.HasFlag(args, "--offline"))
            {
                Console.Error.WriteLine("Offline generate requires EffectiveModel JSON via --from <file>.");
                return 1;
            }

            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var fromFile = ArgUtil.GetOption(args, "--from");
        using var client = CliClientFactory.Create(root, config);

        try
        {
            var models = new List<EffectiveModelDto>();
            if (!string.IsNullOrWhiteSpace(fromFile))
            {
                var json = await File.ReadAllTextAsync(fromFile);
                var model = System.Text.Json.JsonSerializer.Deserialize<EffectiveModelDto>(
                    json,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (model is null)
                {
                    Console.Error.WriteLine("Could not parse EffectiveModel JSON.");
                    return 1;
                }

                models.Add(model);
            }
            else
            {
                var remotes = await client.Definitions.ListAsync(envId);
                foreach (var rem in remotes.Where(r => r.CurrentPublishedVersionId is not null
                                                       && string.Equals(r.Kind, "Entity", StringComparison.OrdinalIgnoreCase)))
                {
                    var model = await client.Definitions.GetEffectiveModelAsync(envId, rem.CurrentPublishedVersionId!.Value);
                    models.Add(model);
                }
            }

            if (models.Count == 0)
            {
                Console.WriteLine("No published entity EffectiveModels to generate.");
                return 0;
            }

            var stale = false;
            foreach (var model in models)
            {
                if (lang is "all" or "ts" or "typescript")
                {
                    var path = Path.Combine(outTs, $"{model.Slug}.ts");
                    var content = TypedStubGenerator.GenerateTypeScript(model);
                    stale |= WriteOrCheck(path, content, check);
                }

                if (lang is "all" or "cs" or "csharp" or "dotnet")
                {
                    var path = Path.Combine(outCs, $"{TypedStubGenerator.ToPascal(model.Slug)}.cs");
                    var content = TypedStubGenerator.GenerateCSharp(model);
                    stale |= WriteOrCheck(path, content, check);
                }
            }

            if (check)
            {
                if (stale)
                {
                    Console.Error.WriteLine("Generated stubs are stale. Run: morph generate");
                    return 2;
                }

                Console.WriteLine("Generated stubs are up to date.");
                return 0;
            }

            Console.WriteLine($"Generated {models.Count} model(s).");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Generate failed: {ex.Message}");
            return 1;
        }
    }

    private static bool WriteOrCheck(string path, string content, bool check)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        if (check)
        {
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"missing {path}");
                return true;
            }

            var existing = File.ReadAllText(path);
            if (!string.Equals(Normalize(existing), Normalize(content), StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"stale   {path}");
                return true;
            }

            return false;
        }

        File.WriteAllText(path, content);
        Console.WriteLine($"wrote {path}");
        return false;
    }

    private static string Normalize(string s) =>
        s.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();
}
