namespace Morph.Cli.Schema;

/// <summary>
/// Canonical YAML IR for an entity/component definition (disk ≡ Authoring API).
/// </summary>
public sealed class DefinitionIr
{
    public string ApiVersion { get; set; } = "morph/v1";

    public string Kind { get; set; } = "Entity";

    public string Slug { get; set; } = "";

    public string DisplayName { get; set; } = "";

    /// <summary>Stable definition id when known (after push/pull).</summary>
    public Guid? DefinitionId { get; set; }

    /// <summary>Latest remote version id when known.</summary>
    public Guid? VersionId { get; set; }

    public List<FieldIr> Fields { get; set; } = [];

    public List<ComponentUseIr> ComponentUses { get; set; } = [];

    public List<ConflictResolutionIr> ConflictResolutions { get; set; } = [];
}

public sealed class FieldIr
{
    public Guid? FieldId { get; set; }

    public string Slug { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string DataType { get; set; } = "String";

    public bool IsRequired { get; set; }

    public bool IsIndexed { get; set; }

    public bool IsUnique { get; set; }

    public bool Encrypt { get; set; }
}

public sealed class ComponentUseIr
{
    public Guid? ComponentUseId { get; set; }

    public string Alias { get; set; } = "";

    /// <summary>Preferred for git-friendly IR; resolved to TargetDefinitionId on push.</summary>
    public string? TargetSlug { get; set; }

    public Guid? TargetDefinitionId { get; set; }

    public Guid? PinnedVersionId { get; set; }

    /// <summary>When true and PinnedVersionId null, pin to current published (or draft) of target.</summary>
    public bool PinLatest { get; set; } = true;

    public string Mode { get; set; } = "Embed";

    public bool IsRequired { get; set; } = true;
}

public sealed class ConflictResolutionIr
{
    public string ConflictPath { get; set; } = "";

    public Guid WinningFieldId { get; set; }
}
