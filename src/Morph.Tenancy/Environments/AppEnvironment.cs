namespace Morph.Tenancy.Environments;

public sealed class AppEnvironment
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Projects.Project? Project { get; set; }

    public required string Name { get; set; }

    public required string Slug { get; set; }

    /// <summary>
    /// Logical Postgres schema name for this environment (Phase 2+).
    /// </summary>
    public string? SchemaName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
