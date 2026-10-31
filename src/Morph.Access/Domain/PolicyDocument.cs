namespace Morph.Access.Domain;

public enum PolicyDocumentStatus
{
    Draft = 0,
    Published = 1,
    Deprecated = 2
}

/// <summary>
/// Versioned PBAC artifact: effect, actions, resources, optional ABAC condition, priority.
/// </summary>
public sealed class PolicyDocument
{
    public required Guid PolicyId { get; init; }

    public required Guid ProjectId { get; init; }

    public Guid? EnvironmentId { get; init; }

    public required string Name { get; init; }

    public required AccessEffect Effect { get; init; }

    public required IReadOnlyList<string> Actions { get; init; }

    /// <summary>
    /// Resource patterns: "entity:{slug}", "entity:{slug}/fields:{path}", "entity:*".
    /// </summary>
    public required IReadOnlyList<string> Resources { get; init; }

    /// <summary>Optional JSONLogic-like condition object.</summary>
    public ConditionExpression? Condition { get; init; }

    /// <summary>Higher priority evaluated first among same effect class.</summary>
    public int Priority { get; init; }

    public PolicyDocumentStatus Status { get; set; } = PolicyDocumentStatus.Draft;

    public int VersionNumber { get; set; } = 1;

    /// <summary>
    /// When Effect=Deny and resource is field-scoped, obligations mask the field instead of denying the whole action.
    /// </summary>
    public bool FieldMaskOnly { get; init; }

    /// <summary>
    /// When true and Effect=Allow with an equality condition on a resource attr vs subject attr,
    /// emit a row-filter obligation for list/read queries.
    /// </summary>
    public bool EmitRowFilterObligation { get; init; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }
}
