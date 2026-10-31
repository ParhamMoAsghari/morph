using System.Text.Json;
using System.Text.Json.Serialization;

namespace Morph.Plugins.Abstractions;

/// <summary>
/// Declares plugin identity, required Morph version, permissions, and marketplace capabilities.
/// Serialized from <c>morph.plugin.json</c> beside the plugin assembly.
/// </summary>
public sealed class PluginManifest
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("displayName")]
    public required string DisplayName { get; init; }

    [JsonPropertyName("author")]
    public string? Author { get; init; }

    [JsonPropertyName("minMorphVersion")]
    public string? MinMorphVersion { get; init; }

    [JsonPropertyName("entryType")]
    public string? EntryType { get; init; }

    [JsonPropertyName("assembly")]
    public string? Assembly { get; init; }

    [JsonPropertyName("permissions")]
    public IReadOnlyList<string> Permissions { get; init; } = [];

    [JsonPropertyName("capabilities")]
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    [JsonPropertyName("configSchema")]
    public JsonElement? ConfigSchema { get; init; }
}
