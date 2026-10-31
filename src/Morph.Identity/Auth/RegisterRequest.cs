using System.ComponentModel.DataAnnotations;

namespace Morph.Identity.Auth;

public sealed class RegisterRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public required string Email { get; init; }

    [Required, MinLength(8), MaxLength(128)]
    public required string Password { get; init; }

    [MaxLength(200)]
    public string? DisplayName { get; init; }
}
