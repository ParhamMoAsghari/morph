using System.Text.Json;
using Morph.Plugins.Loading;

namespace Morph.Cli;

internal static class PluginCommands
{
    public static int Run(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintHelp();
            return args.Length == 0 ? 1 : 0;
        }

        if (!string.Equals(args[0], "plugin", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"Unknown command '{args[0]}'. Try: morph plugin list|install");
            return 1;
        }

        if (args.Length < 2)
        {
            PrintHelp();
            return 1;
        }

        return args[1].ToLowerInvariant() switch
        {
            "list" => List(args.Skip(2).ToArray()),
            "install" => Install(args.Skip(2).ToArray()),
            "help" or "--help" or "-h" => PrintHelpAndExit(),
            _ => UnknownPluginSubcommand(args[1])
        };
    }

    private static int List(string[] args)
    {
        var path = GetOption(args, "--path") ?? ResolveDefaultPluginsPath();
        if (!Directory.Exists(path))
        {
            Console.WriteLine($"No plugins directory at '{Path.GetFullPath(path)}'.");
            return 0;
        }

        var found = 0;
        foreach (var directory in Directory.EnumerateDirectories(path).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var manifestPath = Path.Combine(directory, PluginManifestReader.FileName);
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                var manifest = PluginManifestReader.ReadFile(manifestPath);
                Console.WriteLine($"{manifest.Id,-28} {manifest.Version,-10} {manifest.DisplayName}");
                found++;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to read '{manifestPath}': {ex.Message}");
            }
        }

        if (found == 0)
        {
            Console.WriteLine("No plugins installed.");
        }

        return 0;
    }

    private static int Install(string[] args)
    {
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            Console.Error.WriteLine("Usage: morph plugin install <path-to-addon-folder-or-build-output> [--path <plugins-dir>]");
            return 1;
        }

        var source = Path.GetFullPath(args[0]);
        var targetRoot = GetOption(args, "--path") ?? ResolveDefaultPluginsPath();

        if (!Directory.Exists(source) && !File.Exists(source))
        {
            Console.Error.WriteLine($"Source '{source}' does not exist.");
            return 1;
        }

        string sourceDir;
        if (File.Exists(source) && source.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            sourceDir = Path.GetDirectoryName(source)!;
        }
        else if (Directory.Exists(source))
        {
            sourceDir = source;
        }
        else
        {
            Console.Error.WriteLine("Install source must be a plugin folder or a plugin DLL.");
            return 1;
        }

        var manifestPath = Path.Combine(sourceDir, PluginManifestReader.FileName);
        if (!File.Exists(manifestPath))
        {
            // Allow installing from project dir by pointing at bin output hint
            Console.Error.WriteLine(
                $"No {PluginManifestReader.FileName} in '{sourceDir}'. Build the addon and install its output folder.");
            return 1;
        }

        var manifest = PluginManifestReader.ReadFile(manifestPath);
        var targetDir = Path.Combine(Path.GetFullPath(targetRoot), SanitizeFolderName(manifest.Id));
        Directory.CreateDirectory(targetRoot);

        if (Directory.Exists(targetDir))
        {
            Directory.Delete(targetDir, recursive: true);
        }

        CopyDirectory(sourceDir, targetDir);

        // Ensure Abstractions is not required beside the plugin (shared from host), but copy is fine if present.
        Console.WriteLine($"Installed {manifest.Id}@{manifest.Version} → {targetDir}");
        WriteInstallReceipt(targetDir, manifest.Id, manifest.Version);
        return 0;
    }

    private static void WriteInstallReceipt(string targetDir, string id, string version)
    {
        var receipt = new
        {
            id,
            version,
            installedAt = DateTimeOffset.UtcNow
        };
        File.WriteAllText(
            Path.Combine(targetDir, ".morph-install.json"),
            JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            var name = Path.GetFileName(file);
            if (name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Copy(file, Path.Combine(targetDir, name), overwrite: true);
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDir))
        {
            var name = Path.GetFileName(directory);
            if (name is "ref" or "runtimes")
            {
                continue;
            }

            CopyDirectory(directory, Path.Combine(targetDir, name));
        }
    }

    private static string ResolveDefaultPluginsPath()
    {
        var env = Environment.GetEnvironmentVariable("MORPH_Plugins__Path");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env;
        }

        // Prefer Host content root when running from repo
        var hostPlugins = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Morph.Host", "plugins"));
        if (Directory.Exists(Path.GetDirectoryName(hostPlugins)!))
        {
            return hostPlugins;
        }

        return Path.GetFullPath("./plugins");
    }

    private static string? GetOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static string SanitizeFolderName(string id) =>
        id.Replace('/', '.').Replace('\\', '.');

    private static bool IsHelp(string value) =>
        value is "-h" or "--help" or "help";

    private static int PrintHelpAndExit()
    {
        PrintHelp();
        return 0;
    }

    private static int UnknownPluginSubcommand(string value)
    {
        Console.Error.WriteLine($"Unknown plugin subcommand '{value}'.");
        PrintHelp();
        return 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Morph CLI — plugins

            Usage:
              morph plugin list [--path <plugins-dir>]
              morph plugin install <addon-output-folder> [--path <plugins-dir>]

            Environment:
              MORPH_Plugins__Path   Override plugins directory (default: ./plugins)

            Also see: morph --help
            """);
    }
}
