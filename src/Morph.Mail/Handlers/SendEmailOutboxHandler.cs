using System.Text.Json;
using Morph.Mail.Services;
using Morph.Messaging.Domain;
using Morph.Messaging.Ports;

namespace Morph.Mail.Handlers;

public sealed class SendEmailOutboxHandler : IOutboxHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly MailService _mail;

    public SendEmailOutboxHandler(MailService mail)
    {
        _mail = mail;
    }

    public string MessageType => OutboxMessageTypes.SendEmail;

    public async Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Deserialize<SendEmailPayload>(message.PayloadJson, JsonOptions)
                      ?? throw new InvalidOperationException("Invalid send-email outbox payload.");

        await _mail
            .SendTemplateNowAsync(payload.To, payload.TemplateName, payload.Model, cancellationToken)
            .ConfigureAwait(false);
    }
}
