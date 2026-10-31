namespace Morph.Messaging.Ports;

public interface IWebhookHttpSender
{
    Task SendAsync(
        Uri targetUrl,
        string bodyJson,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default);
}
