using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Morph.Mail.Options;
using Morph.Mail.Ports;

namespace Morph.Mail.Smtp;

public sealed class MailKitMailSender : IMailSender
{
    private readonly MailSenderOptions _options;
    private readonly ILogger<MailKitMailSender> _logger;

    public MailKitMailSender(IOptions<MailSenderOptions> options, ILogger<MailKitMailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(MailMessage message, CancellationToken cancellationToken = default)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(
            _options.FromDisplayName ?? "Morph",
            _options.From));
        mime.To.Add(new MailboxAddress(message.ToDisplayName ?? message.To, message.To));
        mime.Subject = message.Subject;

        var builder = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody ?? string.Empty
        };
        mime.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        var secure = _options.UseStartTls
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.Auto;

        await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, secure, cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken)
                .ConfigureAwait(false);
        }

        await client.SendAsync(mime, cancellationToken).ConfigureAwait(false);
        await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Sent email to {To} subject={Subject}", message.To, message.Subject);
    }
}
