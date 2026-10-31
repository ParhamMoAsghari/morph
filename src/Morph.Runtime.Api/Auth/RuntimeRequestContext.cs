using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Morph.Access.Domain;

namespace Morph.Runtime.Api.Auth;

public static class RuntimeRequestContext
{
    public const string EnvironmentHeader = "X-Morph-Env-Id";

    public static bool TryGetEnvironmentId(HttpContext http, out Guid envId, out string? error)
    {
        envId = default;
        error = null;

        if (http.Request.Headers.TryGetValue(EnvironmentHeader, out var header)
            && Guid.TryParse(header.ToString(), out envId))
        {
            return true;
        }

        if (http.Request.Query.TryGetValue("envId", out var query)
            && Guid.TryParse(query.ToString(), out envId))
        {
            return true;
        }

        var claim = http.User.FindFirst("env_id")?.Value;
        if (Guid.TryParse(claim, out envId))
        {
            return true;
        }

        error = $"Missing environment id. Provide {EnvironmentHeader} header or envId query.";
        return false;
    }

    public static AccessSubject BuildSubject(ClaimsPrincipal user)
    {
        var authenticated = user.Identity?.IsAuthenticated == true;
        var sub = user.FindFirst("sub")?.Value
                  ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? (authenticated ? "unknown" : "anonymous");

        var roles = user.FindAll("role")
            .Concat(user.FindAll(ClaimTypes.Role))
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Unauthenticated data-plane callers evaluate Access as built-in anon.
        if (!authenticated
            || string.Equals(sub, "anonymous", StringComparison.OrdinalIgnoreCase))
        {
            if (!roles.Contains(BuiltInAppRoles.Anon, StringComparer.OrdinalIgnoreCase))
            {
                roles.Add(BuiltInAppRoles.Anon);
            }
        }

        return new AccessSubject
        {
            PrincipalId = sub,
            Roles = roles,
            Attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["sub"] = sub,
                ["email"] = user.FindFirst("email")?.Value ?? user.FindFirst(ClaimTypes.Email)?.Value
            }
        };
    }
}
