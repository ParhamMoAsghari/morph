namespace Morph.Infrastructure.Options;

public sealed class MorphOptions
{
    public const string SectionName = "Morph";

    public ConnectionStringsOptions ConnectionStrings { get; set; } = new();

    public RedisOptions Redis { get; set; } = new();

    public AuthOptions Auth { get; set; } = new();

    public ObjectStorageOptions ObjectStorage { get; set; } = new();

    public MailOptions Mail { get; set; } = new();

    public HangfireOptions Hangfire { get; set; } = new();

    public QuotasOptions Quotas { get; set; } = new();
}

public sealed class QuotasOptions
{
    public int MaxEnvironmentsPerProject { get; set; } = 10;

    public int MaxDefinitionsPerEnvironment { get; set; } = 200;

    public int MaxApiRequestsPerDay { get; set; } = 100_000;

    public int MaxWebhookSubscriptionsPerProject { get; set; } = 50;

    public long MaxStorageBytesPerProject { get; set; } = 5L * 1024 * 1024 * 1024;
}

public sealed class ConnectionStringsOptions
{
    public string Platform { get; set; } = string.Empty;
}

public sealed class RedisOptions
{
    public string? ConnectionString { get; set; }
}

public sealed class AuthOptions
{
    public string Issuer { get; set; } = Morph.Identity.Auth.AuthConstants.Issuer;

    /// <summary>
    /// Confidential OpenIddict client secret for local password-grant skeleton.
    /// Set via Morph:Auth:PlatformClientSecret or MORPH_Auth__PlatformClientSecret — never commit production values.
    /// </summary>
    public string PlatformClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Optional path to a PFX used for OpenIddict signing. When empty, development certificate is used.
    /// </summary>
    public string? SigningCertPath { get; set; }

    public string? SigningCertPassword { get; set; }
}

public sealed class ObjectStorageOptions
{
    public string Endpoint { get; set; } = "http://localhost:9000";

    public string AccessKey { get; set; } = "morphminio";

    public string SecretKey { get; set; } = "morphminio";

    public string BucketPrefix { get; set; } = "morph";

    public bool ForcePathStyle { get; set; } = true;

    public string Region { get; set; } = "us-east-1";
}

public sealed class MailOptions
{
    public string SmtpHost { get; set; } = "localhost";

    public int SmtpPort { get; set; } = 1025;

    public string From { get; set; } = "noreply@morph.local";

    public string? FromDisplayName { get; set; } = "Morph";

    public string PublicBaseUrl { get; set; } = "http://localhost:5100";
}

public sealed class HangfireOptions
{
    public string SchemaName { get; set; } = "hangfire";
}
