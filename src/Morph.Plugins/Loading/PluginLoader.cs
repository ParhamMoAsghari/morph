using System.Reflection;
using Microsoft.Extensions.Logging;
using Morph.Plugins.Abstractions;
using Morph.Plugins.Hosting;
using Morph.Plugins.Store;
using Morph.Plugins.Webhooks;

namespace Morph.Plugins.Loading;

public sealed class PluginLoader
{
    private readonly PluginDiscovery _discovery;
    private readonly IPluginEnablementStore _enablement;
    private readonly IProcessActivityRegistry _activities;
    private readonly ISmsProviderRegistry _smsProviders;
    private readonly IInboundWebhookRegistry _webhooks;
    private readonly IPluginHttpRouteRegistry _httpRoutes;
    private readonly IPluginHttpClientFactory _httpClientFactory;
    private readonly ILogger<PluginLoader> _logger;
    private readonly List<LoadedPlugin> _loaded = [];
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _loadedOnce;

    public PluginLoader(
        PluginDiscovery discovery,
        IPluginEnablementStore enablement,
        IProcessActivityRegistry activities,
        ISmsProviderRegistry smsProviders,
        IInboundWebhookRegistry webhooks,
        IPluginHttpRouteRegistry httpRoutes,
        IPluginHttpClientFactory httpClientFactory,
        ILogger<PluginLoader> logger)
    {
        _discovery = discovery;
        _enablement = enablement;
        _activities = activities;
        _smsProviders = smsProviders;
        _webhooks = webhooks;
        _httpRoutes = httpRoutes;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public IReadOnlyList<LoadedPlugin> Loaded => _loaded;

    public async Task LoadAsync(string pluginsRoot, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_loadedOnce)
            {
                return;
            }

            var discovered = _discovery.Discover(pluginsRoot);
            foreach (var plugin in discovered)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!_enablement.IsEnabled(plugin.Manifest.Id))
                {
                    _logger.LogInformation("Plugin '{PluginId}' is disabled; skipping registration.", plugin.Manifest.Id);
                    continue;
                }

                try
                {
                    var loaded = await LoadOneAsync(plugin, cancellationToken).ConfigureAwait(false);
                    _loaded.Add(loaded);
                    _logger.LogInformation(
                        "Loaded plugin '{PluginId}' v{Version} from '{Path}'.",
                        plugin.Manifest.Id,
                        plugin.Manifest.Version,
                        plugin.AssemblyPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to load plugin '{PluginId}' from '{Path}'.",
                        plugin.Manifest.Id,
                        plugin.AssemblyPath);
                }
            }

            _loadedOnce = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<LoadedPlugin> LoadOneAsync(DiscoveredPlugin discovered, CancellationToken cancellationToken)
    {
        var alc = new PluginLoadContext(discovered.AssemblyPath);
        var assembly = alc.LoadFromAssemblyPath(discovered.AssemblyPath);
        var pluginType = ResolveEntryType(assembly, discovered.Manifest);
        if (Activator.CreateInstance(pluginType) is not IMorphPlugin instance)
        {
            throw new InvalidOperationException(
                $"Type '{pluginType.FullName}' does not implement {nameof(IMorphPlugin)}.");
        }

        // Prefer embedded/runtime manifest when the plugin supplies one; fall back to JSON file.
        var manifest = instance.Manifest.Id == discovered.Manifest.Id
            ? MergeManifest(discovered.Manifest, instance.Manifest)
            : discovered.Manifest;

        var permissions = new HashSet<string>(manifest.Permissions, StringComparer.OrdinalIgnoreCase);
        var builder = new PermissionGatedPluginBuilder(
            manifest.Id,
            permissions,
            _activities,
            _smsProviders,
            _webhooks,
            _httpRoutes,
            _httpClientFactory);

        await instance.RegisterAsync(builder, cancellationToken).ConfigureAwait(false);

        return new LoadedPlugin(manifest, discovered.DirectoryPath, discovered.AssemblyPath, alc, instance, builder);
    }

    private static PluginManifest MergeManifest(PluginManifest file, PluginManifest runtime) =>
        new()
        {
            Id = file.Id,
            Version = string.IsNullOrWhiteSpace(runtime.Version) ? file.Version : runtime.Version,
            DisplayName = string.IsNullOrWhiteSpace(runtime.DisplayName) ? file.DisplayName : runtime.DisplayName,
            Author = runtime.Author ?? file.Author,
            MinMorphVersion = runtime.MinMorphVersion ?? file.MinMorphVersion,
            EntryType = file.EntryType ?? runtime.EntryType,
            Assembly = file.Assembly ?? runtime.Assembly,
            Permissions = runtime.Permissions.Count > 0 ? runtime.Permissions : file.Permissions,
            Capabilities = runtime.Capabilities.Count > 0 ? runtime.Capabilities : file.Capabilities,
            ConfigSchema = runtime.ConfigSchema ?? file.ConfigSchema
        };

    private static Type ResolveEntryType(Assembly assembly, PluginManifest manifest)
    {
        if (!string.IsNullOrWhiteSpace(manifest.EntryType))
        {
            return assembly.GetType(manifest.EntryType, throwOnError: true, ignoreCase: false)
                ?? throw new InvalidOperationException(
                    $"Entry type '{manifest.EntryType}' was not found in '{assembly.FullName}'.");
        }

        var candidates = assembly.GetTypes()
            .Where(t => typeof(IMorphPlugin).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
            .ToArray();

        return candidates.Length switch
        {
            0 => throw new InvalidOperationException(
                $"No {nameof(IMorphPlugin)} implementation found in '{assembly.GetName().Name}'."),
            1 => candidates[0],
            _ => throw new InvalidOperationException(
                $"Multiple {nameof(IMorphPlugin)} implementations found in '{assembly.GetName().Name}'. Set manifest entryType.")
        };
    }
}

public sealed class LoadedPlugin
{
    public LoadedPlugin(
        PluginManifest manifest,
        string directoryPath,
        string assemblyPath,
        PluginLoadContext loadContext,
        IMorphPlugin instance,
        PermissionGatedPluginBuilder builder)
    {
        Manifest = manifest;
        DirectoryPath = directoryPath;
        AssemblyPath = assemblyPath;
        LoadContext = loadContext;
        Instance = instance;
        Builder = builder;
    }

    public PluginManifest Manifest { get; }

    public string DirectoryPath { get; }

    public string AssemblyPath { get; }

    public PluginLoadContext LoadContext { get; }

    public IMorphPlugin Instance { get; }

    public PermissionGatedPluginBuilder Builder { get; }
}
