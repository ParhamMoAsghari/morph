using Morph.Client.Internal;
using Morph.Client.Models;

namespace Morph.Client;

public sealed class AuthClient
{
    private readonly MorphHttp _http;

    internal AuthClient(MorphHttp http) => _http = http;

    public Task<RegisterResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<RegisterResponse>(HttpMethod.Post, "/v1/auth/register", request, cancellationToken: cancellationToken);

    public async Task<TokenResponse> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = email,
            ["password"] = password,
            ["client_id"] = _http.Options.PlatformClientId,
            ["client_secret"] = _http.Options.PlatformClientSecret,
            ["scope"] = _http.Options.Scope
        };

        using var content = new FormUrlEncodedContent(form);
        var text = await _http.SendRawAsync(HttpMethod.Post, "/connect/token", content, applyAuth: false, cancellationToken)
            .ConfigureAwait(false);
        var token = System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(text, JsonDefaults.Options)
                    ?? throw new MorphApiException(200, text, "Invalid token response.");
        _http.Options.AccessToken = token.AccessToken;
        return token;
    }

    public Task<MeResponse> GetMeAsync(CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<MeResponse>(HttpMethod.Get, "/v1/auth/me", cancellationToken: cancellationToken);

    public void SetAccessToken(string accessToken) => _http.Options.AccessToken = accessToken;
}
