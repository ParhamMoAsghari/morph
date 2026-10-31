namespace Morph.Tenancy.Projects;

public sealed class Project
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Organizations.Organization? Organization { get; set; }

    public required string Name { get; set; }

    public required string Slug { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Environments.AppEnvironment> Environments { get; set; } = new List<Environments.AppEnvironment>();
}
