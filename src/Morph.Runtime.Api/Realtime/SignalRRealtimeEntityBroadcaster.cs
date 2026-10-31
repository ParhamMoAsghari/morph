using Microsoft.AspNetCore.SignalR;
using Morph.Messaging.Ports;

namespace Morph.Runtime.Api.Realtime;

public sealed class SignalRRealtimeEntityBroadcaster : IRealtimeEntityBroadcaster
{
    private readonly IHubContext<MorphRealtimeHub> _hub;

    public SignalRRealtimeEntityBroadcaster(IHubContext<MorphRealtimeHub> hub)
    {
        _hub = hub;
    }

    public Task BroadcastAsync(
        Guid environmentId,
        string entitySlug,
        string eventType,
        Guid? recordId,
        object? payload = null,
        CancellationToken cancellationToken = default)
    {
        var group = RealtimeChannels.EntityChannel(environmentId, entitySlug);
        var message = new
        {
            type = eventType,
            environmentId,
            entity = entitySlug,
            recordId,
            payload,
            at = DateTimeOffset.UtcNow
        };

        return _hub.Clients.Group(group).SendAsync("entityEvent", message, cancellationToken);
    }
}
