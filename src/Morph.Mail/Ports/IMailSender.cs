namespace Morph.Mail.Ports;

public interface IMailSender
{
    Task SendAsync(MailMessage message, CancellationToken cancellationToken = default);
}

public sealed class MailMessage
{
    public required string To { get; init; }

    public string? ToDisplayName { get; init; }

    public required string Subject { get; init; }

    public required string HtmlBody { get; init; }

    public string? TextBody { get; init; }
}
