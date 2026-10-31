using System.Text.Json;
using System.Text.Json.Serialization;

namespace Morph.Client.Models;

public sealed record ProcessDefinitionDto(
    Guid DefinitionId,
    Guid EnvironmentId,
    Guid ProjectId,
    string Slug,
    string DisplayName,
    Guid? CurrentPublishedVersionId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ProcessUpsertRequest
{
    [JsonPropertyName("yaml")]
    public string? Yaml { get; set; }

    [JsonPropertyName("documentJson")]
    public string? DocumentJson { get; set; }
}

public sealed record ProcessUpsertResponse(
    Guid DefinitionId,
    Guid VersionId,
    string Slug,
    string Status,
    int VersionNumber);

public sealed record ProcessVersionDto(
    Guid VersionId,
    Guid DefinitionId,
    string Slug,
    string Status,
    int VersionNumber,
    DateTimeOffset? PublishedAt);

public sealed record ProcessAuthoringDto(
    string Slug,
    string? DisplayName,
    Guid? DefinitionId,
    Guid? VersionId,
    JsonElement? Steps);

public sealed record ProcessRunRequest
{
    [JsonPropertyName("input")]
    public Dictionary<string, object?>? Input { get; set; }
}

public sealed record ProcessInstanceDto(
    Guid InstanceId,
    Guid DefinitionId,
    Guid VersionId,
    Guid EnvironmentId,
    Guid ProjectId,
    string Slug,
    string Status,
    string? FailureMessage,
    DateTimeOffset? WaitingUntil,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);

public sealed record ProcessActivityLogDto(
    Guid LogId,
    Guid InstanceId,
    string StepId,
    string ActivityType,
    string Status,
    string? InputJson,
    string? OutputJson,
    string? ErrorMessage,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt);
