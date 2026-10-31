using Morph.Messaging.Domain;

namespace Morph.Messaging.Ports;

public interface IOutboxHandler
{
    string MessageType { get; }

    Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}
