namespace Morph.Identity.Auth;

public sealed record RegisterResponse(Guid UserId, string Email, string? DisplayName);
