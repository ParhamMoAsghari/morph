using System.Collections.Concurrent;
using Morph.Access.Domain;
using Morph.Access.Ports;

namespace Morph.Access.Persistence;

public sealed class InMemoryPolicyStore : IPolicyStore
{
    private readonly ConcurrentDictionary<Guid, PolicyDocument> _policies = new();

    public Task<PolicyDocument?> GetAsync(Guid policyId, CancellationToken cancellationToken = default)
    {
        _policies.TryGetValue(policyId, out var policy);
        return Task.FromResult(policy);
    }

    public Task<IReadOnlyList<PolicyDocument>> ListAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PolicyDocument> items = _policies.Values
            .Where(p => p.ProjectId == projectId)
            .OrderByDescending(p => p.Priority)
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .ToList();
        return Task.FromResult(items);
    }

    public Task<IReadOnlyList<PolicyDocument>> ListPublishedAsync(
        Guid projectId,
        Guid? environmentId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PolicyDocument> items = _policies.Values
            .Where(p => p.ProjectId == projectId
                        && p.Status == PolicyDocumentStatus.Published
                        && (p.EnvironmentId is null || p.EnvironmentId == environmentId))
            .OrderByDescending(p => p.Priority)
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .ToList();
        return Task.FromResult(items);
    }

    public Task SaveAsync(PolicyDocument policy, CancellationToken cancellationToken = default)
    {
        _policies[policy.PolicyId] = policy;
        return Task.CompletedTask;
    }
}
