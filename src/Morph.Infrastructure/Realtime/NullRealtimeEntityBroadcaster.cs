using Morph.Messaging.Ports;

namespace Morph.Infrastructure.Realtime;

/// <summary>Fallback when SignalR hub is not mapped (tests / non-host processes).</summary>
public sealed class NullRealtimeEntityBroadcaster : IRealtimeEntityBroadcaster
{
    public Task BroadcastAsync(
        Guid environmentId,
        string entitySlug,
        string eventType,
        Guid? recordId,
        object? payload = null,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
