namespace Morph.Client;

public sealed class MorphClientOptions
{
    /// <summary>Base URL of Morph.Host, e.g. http://localhost:5100</summary>
    public required Uri BaseAddress { get; init; }

    /// <summary>Optional bearer token applied to every request.</summary>
    public string? AccessToken { get; set; }

    /// <summary>Optional API key (sent as Authorization: Bearer morph_sk_… or X-Morph-Api-Key).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Default environment for data/file calls (X-Morph-Env-Id).</summary>
    public Guid? EnvironmentId { get; set; }

    public string PlatformClientId { get; set; } = "morph-platform";

    public string PlatformClientSecret { get; set; } = "morph-platform-dev-secret";

    public string Scope { get; set; } = "openid profile email offline_access morph.platform";
}
