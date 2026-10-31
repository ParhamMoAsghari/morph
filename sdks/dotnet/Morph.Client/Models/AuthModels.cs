using System.Text.Json.Serialization;

namespace Morph.Client.Models;

public sealed record RegisterRequest(string Email, string Password, string? DisplayName = null);

public sealed record RegisterResponse(Guid Id, string Email, string? DisplayName);

public sealed class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }
}

public sealed class MeResponse
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public bool EmailConfirmed { get; set; }
    public string? DisplayName { get; set; }
}
