using Morph.Access.Domain;
using Morph.Access.Ports;

namespace Morph.Access.Evaluation;

public sealed class RoleHierarchyExpander
{
    private readonly IRoleStore _roles;

    public RoleHierarchyExpander(IRoleStore roles)
    {
        _roles = roles;
    }

    public async Task<IReadOnlySet<string>> ExpandAsync(
        Guid projectId,
        IEnumerable<string> directRoles,
        CancellationToken cancellationToken = default)
    {
        var catalog = await _roles.ListRolesAsync(projectId, cancellationToken).ConfigureAwait(false);
        var inherits = catalog.ToDictionary(
            r => r.Name,
            r => r.Inherits,
            StringComparer.OrdinalIgnoreCase);

        // Fill built-in defaults when catalog is empty / missing edges.
        foreach (var (name, parents) in BuiltInAppRoles.DefaultHierarchy)
        {
            if (!inherits.ContainsKey(name))
            {
                inherits[name] = parents;
            }
        }

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>(directRoles.Where(r => !string.IsNullOrWhiteSpace(r)));

        while (queue.Count > 0)
        {
            var role = queue.Dequeue();
            if (!result.Add(role))
            {
                continue;
            }

            if (inherits.TryGetValue(role, out var children))
            {
                foreach (var child in children)
                {
                    queue.Enqueue(child);
                }
            }
        }

        return result;
    }
}
