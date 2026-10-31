using System.Net;
using System.Net.Sockets;

namespace Morph.BuildingBlocks.Security;

/// <summary>
/// Blocks outbound HTTP(S) to private, loopback, and link-local destinations (SSRF guard).
/// Shared by plugins outbound HTTP and webhook delivery.
/// </summary>
public static class SsrfGuard
{
    public static async Task EnsurePublicEndpointAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        if (!uri.IsAbsoluteUri || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"Blocked non-HTTP(S) URI scheme: {uri.Scheme}");
        }

        if (IPAddress.TryParse(uri.Host, out var literal))
        {
            if (IsBlockedAddress(literal))
            {
                throw new InvalidOperationException($"Blocked private/loopback destination: {uri.Host}");
            }

            return;
        }

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.Host, cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException ex)
        {
            throw new InvalidOperationException($"Failed to resolve host '{uri.Host}'.", ex);
        }

        if (addresses.Length == 0 || addresses.Any(IsBlockedAddress))
        {
            throw new InvalidOperationException($"Blocked private/loopback destination: {uri.Host}");
        }
    }

    public static bool IsBlockedAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] switch
            {
                0 => true,
                10 => true,
                127 => true,
                169 when bytes[1] == 254 => true,
                172 when bytes[1] >= 16 && bytes[1] <= 31 => true,
                192 when bytes[1] == 168 => true,
                _ => false
            };
        }

        return false;
    }
}
