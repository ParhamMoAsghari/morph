namespace Morph.Messaging.Ports;

public interface IAuditStore
{
    Task AppendAsync(AuditEventEntry entry, CancellationToken cancellationToken = default);
}

public sealed class AuditEventEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Action { get; set; }

    public string? ActorPrincipalId { get; set; }

    public Guid? OrganizationId { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? EnvironmentId { get; set; }

    public string? ResourceType { get; set; }

    public string? ResourceId { get; set; }

    public string? DetailJson { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}

public static class AuditActions
{
    public const string DefinitionPublish = "definition.publish";
    public const string MigrationApply = "migration.apply";
    public const string PolicyPublish = "policy.publish";
    public const string CredentialIssue = "credential.issue";
    public const string CredentialRevoke = "credential.revoke";
    public const string WebhookCreate = "webhook.create";
    public const string DefinitionBackupImport = "definition.backup.import";
}
