using Microsoft.Extensions.Logging;
using Morph.Plugins.Abstractions;

namespace Morph.Plugins.Loading;

public sealed class PluginDiscovery
{
    private readonly ILogger<PluginDiscovery> _logger;

    public PluginDiscovery(ILogger<PluginDiscovery> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<DiscoveredPlugin> Discover(string pluginsRoot)
    {
        var root = Path.GetFullPath(pluginsRoot);
        if (!Directory.Exists(root))
        {
            _logger.LogInformation("Plugins path '{Path}' does not exist; skipping discovery.", root);
            return [];
        }

        var results = new List<DiscoveredPlugin>();
        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            var manifestPath = Path.Combine(directory, PluginManifestReader.FileName);
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                var manifest = PluginManifestReader.ReadFile(manifestPath);
                var assemblyPath = ResolveAssemblyPath(directory, manifest);
                if (assemblyPath is null)
                {
                    _logger.LogWarning(
                        "Plugin '{PluginId}' in '{Directory}' has no loadable assembly; skipped.",
                        manifest.Id,
                        directory);
                    continue;
                }

                results.Add(new DiscoveredPlugin(manifest, directory, assemblyPath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read plugin manifest in '{Directory}'.", directory);
            }
        }

        return results;
    }

    private static string? ResolveAssemblyPath(string directory, PluginManifest manifest)
    {
        if (!string.IsNullOrWhiteSpace(manifest.Assembly))
        {
            var configured = Path.Combine(directory, manifest.Assembly);
            return File.Exists(configured) ? configured : null;
        }

        var preferred = Path.Combine(directory, $"{manifest.Id}.dll");
        if (File.Exists(preferred))
        {
            return preferred;
        }

        // Common first-party naming: Morph.Addon.X.dll
        var candidates = Directory.GetFiles(directory, "*.dll")
            .Where(f => !Path.GetFileName(f).StartsWith("Morph.Plugins", StringComparison.OrdinalIgnoreCase))
            .Where(f => !IsSatelliteOrDependency(Path.GetFileName(f)))
            .ToArray();

        return candidates.Length == 1 ? candidates[0] : candidates.FirstOrDefault(LooksLikePluginEntry);
    }

    private static bool IsSatelliteOrDependency(string fileName) =>
        fileName.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
        || fileName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)
        || fileName.Equals("Morph.Plugins.Abstractions.dll", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikePluginEntry(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return name.StartsWith("Morph.Addon.", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("morph.", StringComparison.OrdinalIgnoreCase);
    }
}
