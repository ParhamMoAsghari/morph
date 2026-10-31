using Morph.Access.Domain;
using Morph.Access.Evaluation;
using Morph.DataPlane.Cache;

namespace Morph.Runtime.Api.Realtime;

/// <summary>
/// Access filter for SignalR channel subscribe. Deny when unauthenticated or Access denies <c>subscribe</c>.
/// </summary>
public sealed class RealtimeChannelAuthorizer
{
    private readonly IAccessEvaluator _access;
    private readonly IEnvironmentSchemaResolver _envs;

    public RealtimeChannelAuthorizer(IAccessEvaluator access, IEnvironmentSchemaResolver envs)
    {
        _access = access;
        _envs = envs;
    }

    public async Task<RealtimeAuthResult> AuthorizeSubscribeAsync(
        AccessSubject subject,
        string channel,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subject.PrincipalId)
            || subject.PrincipalId is "anonymous" or "anon")
        {
            return RealtimeAuthResult.Deny("Authentication required to subscribe.");
        }

        if (!RealtimeChannels.TryParseEntityChannel(channel, out var environmentId, out var entitySlug))
        {
            return RealtimeAuthResult.Deny("Invalid channel. Expected env:{id}:entity:{slug}.");
        }

        var env = await _envs.ResolveAsync(environmentId, cancellationToken).ConfigureAwait(false);
        if (env is null)
        {
            return RealtimeAuthResult.Deny("Environment not found.");
        }

        var decision = await _access.EvaluateAsync(
                new AccessContext
                {
                    Subject = subject,
                    Action = AccessActions.Subscribe,
                    Resource = new AccessResource
                    {
                        Type = entitySlug,
                        ProjectId = env.ProjectId,
                        EnvironmentId = environmentId
                    }
                },
                cancellationToken)
            .ConfigureAwait(false);

        return decision.IsDenied
            ? RealtimeAuthResult.Deny(decision.Reason)
            : RealtimeAuthResult.Allow(environmentId, entitySlug, env.ProjectId);
    }
}

public sealed record RealtimeAuthResult(
    bool Allowed,
    string? Reason,
    Guid EnvironmentId,
    string? EntitySlug,
    Guid ProjectId)
{
    public static RealtimeAuthResult Allow(Guid environmentId, string entitySlug, Guid projectId) =>
        new(true, null, environmentId, entitySlug, projectId);

    public static RealtimeAuthResult Deny(string reason) =>
        new(false, reason, Guid.Empty, null, Guid.Empty);
}
