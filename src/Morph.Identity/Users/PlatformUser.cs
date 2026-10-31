using Microsoft.AspNetCore.Identity;

namespace Morph.Identity.Users;

/// <summary>
/// Platform principal — humans operating Morph (orgs, projects, definitions).
/// </summary>
public sealed class PlatformUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }
}
