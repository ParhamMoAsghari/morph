using System.Text.Json;
using Morph.Plugins.Abstractions;

namespace Morph.Plugins.Loading;

public static class PluginManifestReader
{
    public const string FileName = "morph.plugin.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static PluginManifest ReadFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    public static PluginManifest Read(Stream stream)
    {
        var manifest = JsonSerializer.Deserialize<PluginManifest>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("Plugin manifest deserialized to null.");

        Validate(manifest);
        return manifest;
    }

    public static PluginManifest Parse(string json)
    {
        var manifest = JsonSerializer.Deserialize<PluginManifest>(json, SerializerOptions)
            ?? throw new InvalidOperationException("Plugin manifest deserialized to null.");

        Validate(manifest);
        return manifest;
    }

    public static void Validate(PluginManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            throw new InvalidOperationException("Plugin manifest 'id' is required.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            throw new InvalidOperationException($"Plugin '{manifest.Id}' manifest 'version' is required.");
        }

        if (string.IsNullOrWhiteSpace(manifest.DisplayName))
        {
            throw new InvalidOperationException($"Plugin '{manifest.Id}' manifest 'displayName' is required.");
        }

        foreach (var permission in manifest.Permissions)
        {
            if (!PluginPermissions.All.Contains(permission))
            {
                throw new InvalidOperationException(
                    $"Plugin '{manifest.Id}' declares unknown permission '{permission}'.");
            }
        }
    }
}
