namespace Morph.EntityEngine.Domain;

/// <summary>
/// How a parent definition includes another via Embed or Reference.
/// Version is an exact pin — source updates do not silently propagate.
/// </summary>
public sealed class ComponentUse
{
    public required Guid ComponentUseId { get; init; }

    /// <summary>Path segment alias used when embedding (e.g. "person").</summary>
    public required string Alias { get; set; }

    public required Guid TargetDefinitionId { get; set; }

    /// <summary>Exact version pin of the included definition.</summary>
    public required Guid PinnedVersionId { get; set; }

    public ComponentIncludeMode Mode { get; set; } = ComponentIncludeMode.Embed;

    public bool IsRequired { get; set; } = true;

    public ComponentUse Clone() => new()
    {
        ComponentUseId = ComponentUseId,
        Alias = Alias,
        TargetDefinitionId = TargetDefinitionId,
        PinnedVersionId = PinnedVersionId,
        Mode = Mode,
        IsRequired = IsRequired
    };
}
