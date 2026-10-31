namespace Morph.Messaging.Ports;

public interface IRealtimeEntityBroadcaster
{
    Task BroadcastAsync(
        Guid environmentId,
        string entitySlug,
        string eventType,
        Guid? recordId,
        object? payload = null,
        CancellationToken cancellationToken = default);
}

public sealed class RealtimeEntityPayload
{
    public Guid EnvironmentId { get; set; }

    public required string EntitySlug { get; set; }

    public required string EventType { get; set; }

    public Guid? RecordId { get; set; }

    public object? Data { get; set; }
}
