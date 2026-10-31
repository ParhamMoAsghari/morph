using Hangfire.Dashboard;
using System.Security.Claims;

namespace Morph.Infrastructure.Jobs;

/// <summary>
/// Restricts Hangfire dashboard to authenticated platform admins / internal callers.
/// </summary>
public sealed class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        if (http.User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (http.User.IsInRole("platform_owner")
            || http.User.IsInRole("platform_admin")
            || http.User.HasClaim("role", "platform_owner")
            || http.User.HasClaim("role", "platform_admin")
            || http.User.HasClaim(ClaimTypes.Role, "platform_owner")
            || http.User.HasClaim(ClaimTypes.Role, "platform_admin"))
        {
            return true;
        }

        // Local development convenience: allow when remote is loopback AND claim typ=platform.
        var typ = http.User.FindFirst("typ")?.Value;
        var isLoopback = http.Connection.RemoteIpAddress is { } ip
            && (System.Net.IPAddress.IsLoopback(ip) || ip.Equals(http.Connection.LocalIpAddress));

        return isLoopback && string.Equals(typ, "platform", StringComparison.OrdinalIgnoreCase);
    }
}
