using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Morph.Access.Domain;
using Morph.Messaging.Ports;

namespace Morph.Runtime.Api.Realtime;

[Authorize]
public sealed class MorphRealtimeHub : Hub
{
    private readonly RealtimeChannelAuthorizer _authorizer;
    private readonly IUsageCounterStore _usage;

    public MorphRealtimeHub(RealtimeChannelAuthorizer authorizer, IUsageCounterStore usage)
    {
        _authorizer = authorizer;
        _usage = usage;
    }

    public async Task Subscribe(string channel)
    {
        var subject = BuildSubject(Context.User);
        var result = await _authorizer
            .AuthorizeSubscribeAsync(subject, channel, Context.ConnectionAborted)
            .ConfigureAwait(false);

        if (!result.Allowed)
        {
            throw new HubException(result.Reason ?? "Subscribe denied.");
        }

        var group = RealtimeChannels.EntityChannel(result.EnvironmentId, result.EntitySlug!);
        await Groups.AddToGroupAsync(Context.ConnectionId, group).ConfigureAwait(false);

        if (result.ProjectId != Guid.Empty)
        {
            await _usage
                .IncrementAsync(result.ProjectId, UsageMetrics.RealtimeSubscribes, cancellationToken: Context.ConnectionAborted)
                .ConfigureAwait(false);
        }

        await Clients.Caller.SendAsync("subscribed", new { channel = group }).ConfigureAwait(false);
    }

    public async Task Unsubscribe(string channel)
    {
        if (!RealtimeChannels.TryParseEntityChannel(channel, out var environmentId, out var entitySlug))
        {
            throw new HubException("Invalid channel.");
        }

        var group = RealtimeChannels.EntityChannel(environmentId, entitySlug);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, group).ConfigureAwait(false);
        await Clients.Caller.SendAsync("unsubscribed", new { channel = group }).ConfigureAwait(false);
    }

    private static AccessSubject BuildSubject(ClaimsPrincipal? user)
    {
        var sub = user?.FindFirst("sub")?.Value
                  ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? "anonymous";

        var roles = user?.FindAll("role")
            .Concat(user.FindAll(ClaimTypes.Role))
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        return new AccessSubject
        {
            PrincipalId = sub,
            Roles = roles
        };
    }
}
