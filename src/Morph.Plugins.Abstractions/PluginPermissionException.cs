namespace Morph.Plugins.Abstractions;

/// <summary>
/// Thrown when a plugin calls an <see cref="IPluginBuilder"/> API without declaring the required permission.
/// </summary>
public sealed class PluginPermissionException : InvalidOperationException
{
    public PluginPermissionException(string pluginId, string permission, string operation)
        : base($"Plugin '{pluginId}' attempted '{operation}' without permission '{permission}'.")
    {
        PluginId = pluginId;
        Permission = permission;
        Operation = operation;
    }

    public string PluginId { get; }

    public string Permission { get; }

    public string Operation { get; }
}
