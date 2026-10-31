namespace Morph.Plugins.Options;

public sealed class PluginHostOptions
{
    public const string SectionName = "Plugins";

    /// <summary>
    /// Directory scanned for plugin folders (each with morph.plugin.json + DLL).
    /// Bound from <c>Plugins:Path</c> or <c>MORPH_Plugins__Path</c>.
    /// </summary>
    public string Path { get; set; } = "./plugins";

    /// <summary>
    /// When false, discovery still runs but registration is skipped unless the store enables the plugin.
    /// </summary>
    public bool EnableAllDiscovered { get; set; } = true;
}
