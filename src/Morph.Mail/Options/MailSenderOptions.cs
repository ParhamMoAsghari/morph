namespace Morph.Mail.Options;

public sealed class MailSenderOptions
{
    public const string SectionName = "Morph:Mail";

    public string SmtpHost { get; set; } = "localhost";

    public int SmtpPort { get; set; } = 1025;

    public string From { get; set; } = "noreply@morph.local";

    public string? FromDisplayName { get; set; } = "Morph";

    public string? Username { get; set; }

    public string? Password { get; set; }

    public bool UseStartTls { get; set; }

    /// <summary>Public base URL used in verification / reset links.</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:5100";
}
