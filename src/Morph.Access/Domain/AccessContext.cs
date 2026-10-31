namespace Morph.Access.Domain;

/// <summary>
/// Server-built evaluation context. Client-supplied roles/attrs are ignored upstream.
/// </summary>
public sealed class AccessContext
{
    public required AccessSubject Subject { get; init; }

    public required AccessResource Resource { get; init; }

    public required string Action { get; init; }

    public IReadOnlyDictionary<string, object?> Environment { get; init; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}

public sealed class AccessSubject
{
    public required string PrincipalId { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = [];

    public IReadOnlyDictionary<string, object?> Attributes { get; init; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}

public sealed class AccessResource
{
    /// <summary>Entity slug, admin surface, or file bucket pattern.</summary>
    public required string Type { get; init; }

    /// <summary>Optional field path when evaluating field-scoped policies.</summary>
    public string? FieldPath { get; init; }

    public Guid? ProjectId { get; init; }

    public Guid? EnvironmentId { get; init; }

    public Guid? DefinitionId { get; init; }

    public IReadOnlyDictionary<string, object?> Attributes { get; init; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}
