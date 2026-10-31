using Morph.Access.Domain;
using Morph.Access.Ports;
using Morph.BuildingBlocks.Results;
using Morph.BuildingBlocks.Time;

namespace Morph.Access.Services;

public sealed class AccessAdminService
{
    private readonly IRoleStore _roles;
    private readonly IPolicyStore _policies;
    private readonly IClock _clock;

    public AccessAdminService(IRoleStore roles, IPolicyStore policies, IClock clock)
    {
        _roles = roles;
        _policies = policies;
        _clock = clock;
    }

    public Task EnsureBuiltInRolesAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        _roles.EnsureBuiltInRolesAsync(projectId, _clock.UtcNow, cancellationToken);

    public Task<IReadOnlyList<Role>> ListRolesAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        _roles.ListRolesAsync(projectId, cancellationToken);

    public async Task<Result<Role>> CreateRoleAsync(
        Guid projectId,
        string name,
        string? displayName,
        IReadOnlyList<string>? inherits,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Role>(Error.Validation("Role name is required."));
        }

        await EnsureBuiltInRolesAsync(projectId, cancellationToken).ConfigureAwait(false);
        var role = new Role
        {
            RoleId = Guid.NewGuid(),
            ProjectId = projectId,
            Name = name.Trim().ToLowerInvariant(),
            DisplayName = displayName,
            Inherits = inherits ?? [],
            IsBuiltIn = false,
            CreatedAt = _clock.UtcNow
        };
        await _roles.SaveRoleAsync(role, cancellationToken).ConfigureAwait(false);
        return Result.Success(role);
    }

    public async Task<Result<RoleBinding>> BindRoleAsync(
        Guid projectId,
        string principalId,
        string roleName,
        Guid? environmentId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(principalId) || string.IsNullOrWhiteSpace(roleName))
        {
            return Result.Failure<RoleBinding>(Error.Validation("principalId and roleName are required."));
        }

        await EnsureBuiltInRolesAsync(projectId, cancellationToken).ConfigureAwait(false);
        var binding = new RoleBinding
        {
            BindingId = Guid.NewGuid(),
            ProjectId = projectId,
            EnvironmentId = environmentId,
            PrincipalId = principalId.Trim(),
            RoleName = roleName.Trim().ToLowerInvariant(),
            CreatedAt = _clock.UtcNow
        };
        await _roles.SaveBindingAsync(binding, cancellationToken).ConfigureAwait(false);
        return Result.Success(binding);
    }

    public Task<IReadOnlyList<RoleBinding>> ListBindingsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        _roles.ListBindingsAsync(projectId, cancellationToken);

    public Task<IReadOnlyList<PolicyDocument>> ListPoliciesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        _policies.ListAsync(projectId, cancellationToken);

    public async Task<Result<PolicyDocument>> CreatePolicyAsync(
        PolicyDocument draft,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(draft.Name))
        {
            return Result.Failure<PolicyDocument>(Error.Validation("Policy name is required."));
        }

        if (draft.Actions.Count == 0 || draft.Resources.Count == 0)
        {
            return Result.Failure<PolicyDocument>(Error.Validation("Policy requires actions and resources."));
        }

        draft.Status = PolicyDocumentStatus.Draft;
        draft.CreatedAt = _clock.UtcNow;
        await _policies.SaveAsync(draft, cancellationToken).ConfigureAwait(false);
        return Result.Success(draft);
    }

    public async Task<Result<PolicyDocument>> PublishPolicyAsync(
        Guid policyId,
        CancellationToken cancellationToken = default)
    {
        var policy = await _policies.GetAsync(policyId, cancellationToken).ConfigureAwait(false);
        if (policy is null)
        {
            return Result.Failure<PolicyDocument>(Error.NotFound("Policy was not found."));
        }

        if (policy.Status == PolicyDocumentStatus.Published)
        {
            return Result.Success(policy);
        }

        policy.Status = PolicyDocumentStatus.Published;
        policy.PublishedAt = _clock.UtcNow;
        await _policies.SaveAsync(policy, cancellationToken).ConfigureAwait(false);
        return Result.Success(policy);
    }
}
