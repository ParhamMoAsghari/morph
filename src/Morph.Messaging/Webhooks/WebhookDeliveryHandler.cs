using System.Text.Json;
using Morph.BuildingBlocks.Time;
using Morph.Messaging.Domain;
using Morph.Messaging.Ports;

namespace Morph.Messaging.Webhooks;

public sealed class WebhookDeliveryHandler : IOutboxHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IWebhookHttpSender _http;
    private readonly IClock _clock;

    public WebhookDeliveryHandler(IWebhookHttpSender http, IClock clock)
    {
        _http = http;
        _clock = clock;
    }

    public string MessageType => OutboxMessageTypes.WebhookDeliver;

    public async Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Deserialize<WebhookDeliveryPayload>(message.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("Invalid webhook delivery payload.");

        if (string.IsNullOrWhiteSpace(payload.TargetUrl) || string.IsNullOrWhiteSpace(payload.SigningSecret))
        {
            throw new InvalidOperationException("Webhook delivery requires target URL and signing secret.");
        }

        if (!Uri.TryCreate(payload.TargetUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Invalid webhook URL: {payload.TargetUrl}");
        }

        var signature = WebhookSignature.Sign(payload.SigningSecret, payload.BodyJson, _clock.UtcNow);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [WebhookSignature.HeaderName] = signature,
            ["X-Morph-Event"] = payload.EventType,
            ["X-Morph-Delivery-Id"] = message.Id.ToString("N"),
            ["Content-Type"] = "application/json"
        };

        await _http.SendAsync(uri, payload.BodyJson, headers, cancellationToken).ConfigureAwait(false);
    }
}
