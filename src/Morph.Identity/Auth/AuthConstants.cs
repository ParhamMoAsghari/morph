namespace Morph.Identity.Auth;

public static class AuthConstants
{
    public const string Issuer = "https://localhost:5101/";

    /// <summary>
    /// Confidential local/dev client used to obtain tokens via password grant (Phase 0 skeleton only).
    /// Password grant is not enabled for public clients.
    /// </summary>
    public const string PlatformClientId = "morph-platform";

    public const string ScopeOpenId = "openid";
    public const string ScopeProfile = "profile";
    public const string ScopeEmail = "email";
    public const string ScopeOfflineAccess = "offline_access";
    public const string ScopePlatformApi = "morph.platform";

    public const string ClaimOrgId = "org_id";
    public const string ClaimProjectId = "project_id";
    public const string ClaimEnvId = "env_id";
    public const string ClaimPrincipalType = "typ";

    public const string PrincipalTypePlatform = "platform";
}
