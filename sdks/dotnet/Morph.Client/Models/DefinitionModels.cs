namespace Morph.Client.Models;

public sealed class DefinitionSummary
{
    public Guid DefinitionId { get; set; }
    public Guid EnvironmentId { get; set; }
    public string Slug { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Kind { get; set; } = "Entity";
    public Guid? CurrentPublishedVersionId { get; set; }
}

public sealed class FieldDto
{
    public Guid FieldId { get; set; }
    public string Slug { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string DataType { get; set; } = "String";
    public bool IsRequired { get; set; }
    public bool IsIndexed { get; set; }
    public bool IsUnique { get; set; }
    public bool Encrypt { get; set; }
}

public sealed class ComponentUseDto
{
    public Guid ComponentUseId { get; set; }
    public string Alias { get; set; } = "";
    public Guid TargetDefinitionId { get; set; }
    public Guid PinnedVersionId { get; set; }
    public string Mode { get; set; } = "Embed";
    public bool IsRequired { get; set; } = true;
}

public sealed class ConflictResolutionDto
{
    public string ConflictPath { get; set; } = "";
    public Guid WinningFieldId { get; set; }
}

public sealed record CreateDefinitionRequest(
    string Slug,
    string DisplayName,
    string Kind,
    IReadOnlyList<FieldDto>? Fields);

public sealed record ComposeDefinitionRequest(
    string Slug,
    string DisplayName,
    string Kind,
    IReadOnlyList<ComponentUseDto>? ComponentUses,
    IReadOnlyList<FieldDto>? LocalFields,
    IReadOnlyList<ConflictResolutionDto>? ConflictResolutions);

public sealed class CreateDefinitionResponse
{
    public Guid DefinitionId { get; set; }
    public Guid VersionId { get; set; }
    public string Slug { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Status { get; set; } = "";
}

public sealed class DefinitionVersionDto
{
    public Guid VersionId { get; set; }
    public Guid DefinitionId { get; set; }
    public Guid EnvironmentId { get; set; }
    public string Slug { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Kind { get; set; } = "";
    public int VersionNumber { get; set; }
    public string Status { get; set; } = "";
    public List<FieldDto> Fields { get; set; } = [];
    public List<ComponentUseDto> ComponentUses { get; set; } = [];
    public List<ConflictResolutionDto> ConflictResolutions { get; set; } = [];
    public string? EffectiveModelFingerprint { get; set; }
}

public sealed class EffectiveModelDto
{
    public Guid DefinitionId { get; set; }
    public Guid VersionId { get; set; }
    public Guid EnvironmentId { get; set; }
    public string Slug { get; set; } = "";
    public string Kind { get; set; } = "";
    public int VersionNumber { get; set; }
    public string Fingerprint { get; set; } = "";
    public List<EffectiveFieldDto> Fields { get; set; } = [];
}

public sealed class EffectiveFieldDto
{
    public Guid FieldId { get; set; }
    public string Path { get; set; } = "";
    public string Slug { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string DataType { get; set; } = "String";
    public bool IsRequired { get; set; }
}
