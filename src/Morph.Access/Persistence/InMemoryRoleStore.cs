using System.Collections.Concurrent;
using Morph.Access.Domain;
using Morph.Access.Ports;

namespace Morph.Access.Persistence;

public sealed class InMemoryRoleStore : IRoleStore
{
    private readonly ConcurrentDictionary<Guid, Role> _roles = new();
    private readonly ConcurrentDictionary<Guid, RoleBinding> _bindings = new();

    public Task<IReadOnlyList<Role>> ListRolesAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Role> items = _roles.Values
            .Where(r => r.ProjectId == projectId)
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Task.FromResult(items);
    }

    public Task SaveRoleAsync(Role role, CancellationToken cancellationToken = default)
    {
        _roles[role.RoleId] = role;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RoleBinding>> ListBindingsForPrincipalAsync(
        Guid projectId,
        string principalId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<RoleBinding> items = _bindings.Values
            .Where(b => b.ProjectId == projectId
                        && string.Equals(b.PrincipalId, principalId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult(items);
    }

    public Task<IReadOnlyList<RoleBinding>> ListBindingsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<RoleBinding> items = _bindings.Values
            .Where(b => b.ProjectId == projectId)
            .ToList();
        return Task.FromResult(items);
    }

    public Task SaveBindingAsync(RoleBinding binding, CancellationToken cancellationToken = default)
    {
        _bindings[binding.BindingId] = binding;
        return Task.CompletedTask;
    }

    public async Task EnsureBuiltInRolesAsync(
        Guid projectId,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default)
    {
        var existing = await ListRolesAsync(projectId, cancellationToken).ConfigureAwait(false);
        var names = existing.Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var role in BuiltInAppRoles.CreateDefaults(projectId, createdAt))
        {
            if (!names.Contains(role.Name))
            {
                await SaveRoleAsync(role, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
