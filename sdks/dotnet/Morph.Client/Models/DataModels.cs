using System.Text.Json;

namespace Morph.Client.Models;

public sealed class EntityRecordDto
{
    public Guid Id { get; set; }
    public string Entity { get; set; } = "";
    public Guid DefinitionVersionId { get; set; }
    public Dictionary<string, JsonElement> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class PageDto<T>
{
    public List<T> Items { get; set; } = [];
    public string? NextCursor { get; set; }
    public bool HasMore { get; set; }
}

public sealed class MigrationPlanDto
{
    public Guid PlanId { get; set; }
    public Guid DefinitionId { get; set; }
    public Guid? FromVersionId { get; set; }
    public Guid ToVersionId { get; set; }
    public string? Table { get; set; }
    public string? MappingFingerprint { get; set; }
    public List<MigrationStageDto> Stages { get; set; } = [];
}

public sealed class MigrationStageDto
{
    public int Ordinal { get; set; }
    public string Kind { get; set; } = "";
    public string Stage { get; set; } = "";
    public string? Sql { get; set; }
}

public sealed class MigrationRunDto
{
    public Guid RunId { get; set; }
    public Guid PlanId { get; set; }
    public Guid DefinitionId { get; set; }
    public string Status { get; set; } = "";
    public string? FailureMessage { get; set; }
}

public sealed class FileBucketDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid EnvironmentId { get; set; }
    public string Slug { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

public sealed class UploadSessionDto
{
    public Guid FileId { get; set; }
    public string UploadUrl { get; set; } = "";
    public Dictionary<string, string>? Headers { get; set; }
}

public sealed class DownloadUrlDto
{
    public Guid FileId { get; set; }
    public string DownloadUrl { get; set; } = "";
    public DateTimeOffset? ExpiresAt { get; set; }
}
