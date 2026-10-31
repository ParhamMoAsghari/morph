namespace Morph.Infrastructure.Persistence.Audit;

public sealed class AuditEventRecord
{
    public Guid Id { get; set; }

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
