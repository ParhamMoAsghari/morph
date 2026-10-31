using System.Reflection;
using System.Runtime.Loader;

namespace Morph.Plugins.Loading;

/// <summary>
/// Collectible ALC so plugins can be unloaded when disabled (best-effort).
/// Shared Morph.Plugins.Abstractions types resolve from the default context.
/// </summary>
public sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginMainAssemblyPath)
        : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginMainAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (IsSharedHostAssembly(assemblyName.Name))
        {
            return null;
        }

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }

    private static bool IsSharedHostAssembly(string? name) =>
        name is "Morph.Plugins.Abstractions"
            or "Morph.Plugins"
            or "Microsoft.AspNetCore"
            or "Microsoft.Extensions.DependencyInjection.Abstractions"
            || name?.StartsWith("Microsoft.AspNetCore.", StringComparison.Ordinal) == true
            || name?.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal) == true
            || name?.StartsWith("System.", StringComparison.Ordinal) == true;
}
