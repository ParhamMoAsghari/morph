using Morph.Plugins.Abstractions;

namespace Morph.Plugins.Loading;

public sealed record DiscoveredPlugin(
    PluginManifest Manifest,
    string DirectoryPath,
    string AssemblyPath);
