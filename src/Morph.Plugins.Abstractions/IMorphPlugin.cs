namespace Morph.Plugins.Abstractions;

/// <summary>
/// Entrypoint implemented by every Morph addon. Discovered via manifest <c>entryType</c>.
/// </summary>
public interface IMorphPlugin
{
    PluginManifest Manifest { get; }

    Task RegisterAsync(IPluginBuilder builder, CancellationToken cancellationToken);
}
