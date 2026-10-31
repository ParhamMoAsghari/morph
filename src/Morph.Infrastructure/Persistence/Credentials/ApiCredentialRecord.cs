namespace Morph.Infrastructure.Persistence.Credentials;

public sealed class ApiCredentialRecord
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Guid? EnvironmentId { get; set; }

    public required string Name { get; set; }

    /// <summary>Public prefix, e.g. morph_sk_ab12…</summary>
    public required string KeyPrefix { get; set; }

    /// <summary>SHA-256 hex of the full secret.</summary>
    public required string SecretHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? CreatedByPrincipalId { get; set; }
}