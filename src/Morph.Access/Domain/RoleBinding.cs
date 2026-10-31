namespace Morph.Access.Domain;

public sealed class RoleBinding
{
    public required Guid BindingId { get; init; }

    public required Guid ProjectId { get; init; }

    public Guid? EnvironmentId { get; init; }

    public required string PrincipalId { get; init; }

    public required string RoleName { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
