namespace Morph.Tenancy.Memberships;

/// <summary>
/// Links a platform user to an organization (Phase 0 stub — roles expanded in Phase 3).
/// </summary>
public sealed class OrganizationMembership
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Organizations.Organization? Organization { get; set; }

    public Guid UserId { get; set; }

    public required string Role { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
