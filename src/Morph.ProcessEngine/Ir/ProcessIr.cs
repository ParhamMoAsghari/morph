using System.Text.Json;
using System.Text.Json.Serialization;

namespace Morph.ProcessEngine.Ir;

/// <summary>
/// Canonical Morph process authoring IR (YAML ↔ JSON).
/// </summary>
public sealed class ProcessDocument
{
    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; } = "morph/v1";

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "Process";

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("definitionId")]
    public Guid? DefinitionId { get; set; }

    [JsonPropertyName("versionId")]
    public Guid? VersionId { get; set; }

    [JsonPropertyName("input")]
    public List<ProcessVariableDecl> Input { get; set; } = [];

    [JsonPropertyName("variables")]
    public List<ProcessVariableDecl> Variables { get; set; } = [];

    [JsonPropertyName("steps")]
    public List<ProcessStepIr> Steps { get; set; } = [];
}

public sealed class ProcessVariableDecl
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "json";

    [JsonPropertyName("default")]
    public JsonElement? Default { get; set; }
}

public sealed class ProcessStepIr
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("with")]
    public Dictionary<string, JsonElement>? With { get; set; }

    [JsonPropertyName("steps")]
    public List<ProcessStepIr>? Steps { get; set; }

    [JsonPropertyName("then")]
    public List<ProcessStepIr>? Then { get; set; }

    [JsonPropertyName("else")]
    public List<ProcessStepIr>? Else { get; set; }
}

/// <summary>Compiled form stored on publish — flat executable graph.</summary>
public sealed class CompiledProcess
{
    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";

    [JsonPropertyName("versionId")]
    public Guid VersionId { get; set; }

    [JsonPropertyName("steps")]
    public List<CompiledStep> Steps { get; set; } = [];

    [JsonPropertyName("input")]
    public List<ProcessVariableDecl> Input { get; set; } = [];

    [JsonPropertyName("variables")]
    public List<ProcessVariableDecl> Variables { get; set; } = [];
}

public sealed class CompiledStep
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("with")]
    public Dictionary<string, JsonElement>? With { get; set; }

    [JsonPropertyName("then")]
    public List<CompiledStep>? Then { get; set; }

    [JsonPropertyName("else")]
    public List<CompiledStep>? Else { get; set; }

    [JsonPropertyName("steps")]
    public List<CompiledStep>? Steps { get; set; }
}
