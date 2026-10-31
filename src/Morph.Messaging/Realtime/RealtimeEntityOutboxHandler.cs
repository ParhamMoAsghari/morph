using System.Text.Json;
using Morph.Messaging.Domain;
using Morph.Messaging.Ports;

namespace Morph.Messaging.Realtime;

public sealed class RealtimeEntityOutboxHandler : IOutboxHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRealtimeEntityBroadcaster _broadcaster;

    public RealtimeEntityOutboxHandler(IRealtimeEntityBroadcaster broadcaster)
    {
        _broadcaster = broadcaster;
    }

    public string MessageType => OutboxMessageTypes.RealtimeEntity;

    public async Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Deserialize<RealtimeEntityPayload>(message.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("Invalid realtime entity payload.");

        await _broadcaster
            .BroadcastAsync(
                payload.EnvironmentId,
                payload.EntitySlug,
                payload.EventType,
                payload.RecordId,
                payload.Data,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
