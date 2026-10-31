namespace Morph.Access.Domain;

public sealed class AccessDecision
{
    public required AccessEffect Effect { get; init; }

    public required string Reason { get; init; }

    public AccessObligations Obligations { get; init; } = AccessObligations.Empty;

    public IReadOnlyList<string> MatchedPolicyIds { get; init; } = [];

    public bool IsAllowed => Effect == AccessEffect.Allow;

    public bool IsDenied => Effect == AccessEffect.Deny;

    public static AccessDecision Allow(string reason, AccessObligations? obligations = null, params string[] matchedPolicies) =>
        new()
        {
            Effect = AccessEffect.Allow,
            Reason = reason,
            Obligations = obligations ?? AccessObligations.Empty,
            MatchedPolicyIds = matchedPolicies
        };

    public static AccessDecision Deny(string reason, params string[] matchedPolicies) =>
        new()
        {
            Effect = AccessEffect.Deny,
            Reason = reason,
            Obligations = AccessObligations.Empty,
            MatchedPolicyIds = matchedPolicies
        };
}
