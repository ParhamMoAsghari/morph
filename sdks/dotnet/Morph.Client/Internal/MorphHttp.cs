using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Morph.Client.Internal;

namespace Morph.Client.Internal;

internal sealed class MorphHttp
{
    private readonly HttpClient _http;
    private readonly MorphClientOptions _options;

    public MorphHttp(HttpClient http, MorphClientOptions options)
    {
        _http = http;
        _options = options;
        if (_http.BaseAddress is null)
        {
            _http.BaseAddress = options.BaseAddress;
        }
    }

    public MorphClientOptions Options => _options;

    public void ApplyAuth(HttpRequestMessage request)
    {
        var token = _options.AccessToken ?? _options.ApiKey;
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public void ApplyEnv(HttpRequestMessage request, Guid? environmentId = null)
    {
        var env = environmentId ?? _options.EnvironmentId;
        if (env is { } id)
        {
            request.Headers.TryAddWithoutValidation("X-Morph-Env-Id", id.ToString("D"));
        }
    }

    public async Task<T> SendJsonAsync<T>(
        HttpMethod method,
        string path,
        object? body = null,
        Guid? environmentId = null,
        bool includeEnv = false,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, path);
        ApplyAuth(request);
        if (includeEnv)
        {
            ApplyEnv(request, environmentId);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonDefaults.Options);
        }

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new MorphApiException((int)response.StatusCode, text);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return default!;
        }

        return JsonSerializer.Deserialize<T>(text, JsonDefaults.Options)
               ?? throw new MorphApiException((int)response.StatusCode, text, "Failed to deserialize response.");
    }

    public async Task SendAsync(
        HttpMethod method,
        string path,
        object? body = null,
        Guid? environmentId = null,
        bool includeEnv = false,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, path);
        ApplyAuth(request);
        if (includeEnv)
        {
            ApplyEnv(request, environmentId);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonDefaults.Options);
        }

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new MorphApiException((int)response.StatusCode, text);
        }
    }

    public async Task<string> SendRawAsync(
        HttpMethod method,
        string path,
        HttpContent? content = null,
        bool applyAuth = true,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        if (applyAuth)
        {
            ApplyAuth(request);
        }

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new MorphApiException((int)response.StatusCode, text);
        }

        return text;
    }

    public async Task<JsonElement> SendJsonElementAsync(
        HttpMethod method,
        string path,
        object? body = null,
        Guid? environmentId = null,
        bool includeEnv = false,
        CancellationToken cancellationToken = default)
    {
        var text = await SendRawJsonAsync(method, path, body, environmentId, includeEnv, cancellationToken)
            .ConfigureAwait(false);
        return JsonSerializer.Deserialize<JsonElement>(text, JsonDefaults.Options);
    }

    public async Task<string> SendRawJsonAsync(
        HttpMethod method,
        string path,
        object? body = null,
        Guid? environmentId = null,
        bool includeEnv = false,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, path);
        ApplyAuth(request);
        if (includeEnv)
        {
            ApplyEnv(request, environmentId);
        }

        if (body is not null)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(body, JsonDefaults.Options),
                Encoding.UTF8,
                "application/json");
        }

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new MorphApiException((int)response.StatusCode, text);
        }

        return text;
    }
}
