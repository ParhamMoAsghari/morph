using System.Text.Json;
using System.Text.Json.Serialization;
using Morph.EntityEngine.Domain;

namespace Morph.Infrastructure.Persistence.EntityEngine;

internal static class EntityEngineJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static string SerializeAuthoring(DefinitionVersion version) =>
        JsonSerializer.Serialize(
            new AuthoringPayload(
                version.Fields,
                version.ComponentUses,
                version.Relationships,
                version.ConflictResolutions),
            Options);

    public static void ApplyAuthoring(DefinitionVersion version, string json)
    {
        var payload = JsonSerializer.Deserialize<AuthoringPayload>(json, Options)
                      ?? new AuthoringPayload([], [], [], []);
        version.Fields = payload.Fields?.ToList() ?? [];
        version.ComponentUses = payload.ComponentUses?.ToList() ?? [];
        version.Relationships = payload.Relationships?.ToList() ?? [];
        version.ConflictResolutions = payload.ConflictResolutions?.ToList() ?? [];
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);

    private sealed record AuthoringPayload(
        IReadOnlyList<FieldDefinition>? Fields,
        IReadOnlyList<ComponentUse>? ComponentUses,
        IReadOnlyList<RelationshipDefinition>? Relationships,
        IReadOnlyList<ConflictResolution>? ConflictResolutions);
}
