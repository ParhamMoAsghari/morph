namespace Morph.Access.Domain;

public sealed class Role
{
    public required Guid RoleId { get; init; }

    public required Guid ProjectId { get; init; }

    public required string Name { get; init; }

    public string? DisplayName { get; init; }

    /// <summary>Role names this role inherits (explicit hierarchy).</summary>
    public IReadOnlyList<string> Inherits { get; init; } = [];

    public bool IsBuiltIn { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public static class BuiltInAppRoles
{
    public const string Owner = "owner";
    public const string Editor = "editor";
    public const string Viewer = "viewer";
    public const string Authenticated = "authenticated";
    public const string Anon = "anon";

    /// <summary>owner ⊇ editor ⊇ viewer ⊇ authenticated.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> DefaultHierarchy { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [Owner] = [Editor],
            [Editor] = [Viewer],
            [Viewer] = [Authenticated],
            [Authenticated] = [],
            [Anon] = []
        };

    public static IReadOnlyList<Role> CreateDefaults(Guid projectId, DateTimeOffset createdAt) =>
    [
        new Role
        {
            RoleId = Guid.NewGuid(),
            ProjectId = projectId,
            Name = Owner,
            DisplayName = "Owner",
            Inherits = DefaultHierarchy[Owner],
            IsBuiltIn = true,
            CreatedAt = createdAt
        },
        new Role
        {
            RoleId = Guid.NewGuid(),
            ProjectId = projectId,
            Name = Editor,
            DisplayName = "Editor",
            Inherits = DefaultHierarchy[Editor],
            IsBuiltIn = true,
            CreatedAt = createdAt
        },
        new Role
        {
            RoleId = Guid.NewGuid(),
            ProjectId = projectId,
            Name = Viewer,
            DisplayName = "Viewer",
            Inherits = DefaultHierarchy[Viewer],
            IsBuiltIn = true,
            CreatedAt = createdAt
        },
        new Role
        {
            RoleId = Guid.NewGuid(),
            ProjectId = projectId,
            Name = Authenticated,
            DisplayName = "Authenticated",
            Inherits = DefaultHierarchy[Authenticated],
            IsBuiltIn = true,
            CreatedAt = createdAt
        },
        new Role
        {
            RoleId = Guid.NewGuid(),
            ProjectId = projectId,
            Name = Anon,
            DisplayName = "Anonymous",
            Inherits = DefaultHierarchy[Anon],
            IsBuiltIn = true,
            CreatedAt = createdAt
        }
    ];
}
