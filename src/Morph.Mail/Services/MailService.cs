using Morph.Mail.Ports;
using Morph.Mail.Templates;
using Morph.Messaging.Domain;
using Morph.Messaging.Outbox;

namespace Morph.Mail.Services;

public sealed class MailService
{
    private readonly IOutboxPublisher _outbox;
    private readonly IEmailTemplateRenderer _templates;
    private readonly IMailSender _sender;

    public MailService(
        IOutboxPublisher outbox,
        IEmailTemplateRenderer templates,
        IMailSender sender)
    {
        _outbox = outbox;
        _templates = templates;
        _sender = sender;
    }

    public Task EnqueueTemplateAsync(
        string to,
        string templateName,
        IReadOnlyDictionary<string, object?> model,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new SendEmailPayload
        {
            To = to,
            TemplateName = templateName,
            Model = model.ToDictionary(kv => kv.Key, kv => kv.Value)
        };

        return _outbox.PublishAsync(
            OutboxMessageTypes.SendEmail,
            payload,
            correlationId,
            cancellationToken: cancellationToken);
    }

    public async Task SendTemplateNowAsync(
        string to,
        string templateName,
        IReadOnlyDictionary<string, object?> model,
        CancellationToken cancellationToken = default)
    {
        var (subject, html, text) = await _templates
            .RenderAsync(templateName, model, cancellationToken)
            .ConfigureAwait(false);

        await _sender.SendAsync(
                new MailMessage
                {
                    To = to,
                    Subject = subject,
                    HtmlBody = html,
                    TextBody = text
                },
                cancellationToken)
            .ConfigureAwait(false);
    }
}

public sealed class SendEmailPayload
{
    public required string To { get; init; }

    public required string TemplateName { get; init; }

    public Dictionary<string, object?> Model { get; init; } = new();
}
