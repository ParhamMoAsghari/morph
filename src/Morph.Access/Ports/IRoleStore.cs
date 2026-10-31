using Morph.Access.Domain;

namespace Morph.Access.Ports;

public interface IRoleStore
{
    Task<IReadOnlyList<Role>> ListRolesAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task SaveRoleAsync(Role role, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleBinding>> ListBindingsForPrincipalAsync(
        Guid projectId,
        string principalId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleBinding>> ListBindingsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task SaveBindingAsync(RoleBinding binding, CancellationToken cancellationToken = default);

    Task EnsureBuiltInRolesAsync(Guid projectId, DateTimeOffset createdAt, CancellationToken cancellationToken = default);
}
