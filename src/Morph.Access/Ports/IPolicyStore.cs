using Morph.Access.Domain;

namespace Morph.Access.Ports;

public interface IPolicyStore
{
    Task<PolicyDocument?> GetAsync(Guid policyId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PolicyDocument>> ListAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PolicyDocument>> ListPublishedAsync(
        Guid projectId,
        Guid? environmentId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(PolicyDocument policy, CancellationToken cancellationToken = default);
}
