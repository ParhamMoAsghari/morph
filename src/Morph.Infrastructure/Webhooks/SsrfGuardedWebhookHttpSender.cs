using System.Net.Http.Headers;
using System.Text;
using Morph.BuildingBlocks.Security;
using Morph.Messaging.Ports;

namespace Morph.Infrastructure.Webhooks;

public sealed class SsrfGuardedWebhookHttpSender : IWebhookHttpSender
{
    public const string ClientName = "Morph.Webhooks.Outbound";

    private readonly IHttpClientFactory _httpClientFactory;

    public SsrfGuardedWebhookHttpSender(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task SendAsync(
        Uri targetUrl,
        string bodyJson,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        await SsrfGuard.EnsurePublicEndpointAsync(targetUrl, cancellationToken).ConfigureAwait(false);

        var client = _httpClientFactory.CreateClient(ClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, targetUrl)
        {
            Content = new StringContent(bodyJson, Encoding.UTF8, "application/json")
        };

        foreach (var (key, value) in headers)
        {
            if (key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            request.Headers.TryAddWithoutValidation(key, value);
        }

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Webhook POST failed with {(int)response.StatusCode}: {Truncate(body, 500)}");
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}

/// <summary>Delegating handler that reuses shared SSRF checks for webhook HttpClient.</summary>
public sealed class WebhookSsrfHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri is null)
        {
            throw new InvalidOperationException("Webhook requests require an absolute URI.");
        }

        await SsrfGuard.EnsurePublicEndpointAsync(request.RequestUri, cancellationToken).ConfigureAwait(false);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
