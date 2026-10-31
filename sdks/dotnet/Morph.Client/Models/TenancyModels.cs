namespace Morph.Client.Models;

public sealed record CreateOrganizationRequest(string Name, string Slug);

public sealed class OrganizationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
}

public sealed record CreateProjectRequest(Guid OrganizationId, string Name, string Slug);

public sealed class ProjectDto
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
}

public sealed record CreateEnvironmentRequest(string Name, string Slug);

public sealed class EnvironmentDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? SchemaName { get; set; }
}
