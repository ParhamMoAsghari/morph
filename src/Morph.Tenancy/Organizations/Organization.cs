namespace Morph.Tenancy.Organizations;

public sealed class Organization
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public required string Slug { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Projects.Project> Projects { get; set; } = new List<Projects.Project>();
}
