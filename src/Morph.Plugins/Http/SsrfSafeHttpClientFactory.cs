using Morph.BuildingBlocks.Security;
using Morph.Plugins.Abstractions;

namespace Morph.Plugins.Http;

/// <summary>
/// Wraps <see cref="IHttpClientFactory"/> and blocks requests to private/link-local/loopback addresses.
/// </summary>
public sealed class SsrfSafeHttpClientFactory : IPluginHttpClientFactory
{
    public const string ClientName = "Morph.Plugins.Outbound";

    private readonly IHttpClientFactory _httpClientFactory;

    public SsrfSafeHttpClientFactory(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public HttpClient CreateClient(string? name = null) =>
        _httpClientFactory.CreateClient(name ?? ClientName);
}

public sealed class SsrfGuardHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri is null)
        {
            throw new InvalidOperationException("Outbound plugin HTTP requests require an absolute URI.");
        }

        await SsrfGuard.EnsurePublicEndpointAsync(request.RequestUri, cancellationToken).ConfigureAwait(false);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Forwarding shim — prefer <see cref="SsrfGuard.EnsurePublicEndpointAsync"/>.</summary>
    public static Task EnsurePublicEndpointAsync(Uri uri, CancellationToken cancellationToken) =>
        SsrfGuard.EnsurePublicEndpointAsync(uri, cancellationToken);
}
