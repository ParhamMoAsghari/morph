using Morph.Access.Domain;
using Morph.Access.Ports;

namespace Morph.Access.Evaluation;

/// <summary>
/// Unified RBAC + ABAC + PBAC evaluator.
/// Rules: explicit Deny wins; else Allow if any matching Allow; else default Deny.
/// Field-mask Deny policies become obligations on Allow (do not deny the whole action).
/// </summary>
public sealed class AccessEvaluator : IAccessEvaluator
{
    private readonly IRoleStore _roles;
    private readonly IPolicyStore _policies;
    private readonly RoleHierarchyExpander _hierarchy;

    public AccessEvaluator(IRoleStore roles, IPolicyStore policies, RoleHierarchyExpander hierarchy)
    {
        _roles = roles;
        _policies = policies;
        _hierarchy = hierarchy;
    }

    public async Task<AccessDecision> EvaluateAsync(
        AccessContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Action))
        {
            return AccessDecision.Deny("Action is required.");
        }

        var projectId = context.Resource.ProjectId;
        if (projectId is null || projectId == Guid.Empty)
        {
            return AccessDecision.Deny("Resource.ProjectId is required.");
        }

        var bindings = await _roles
            .ListBindingsForPrincipalAsync(projectId.Value, context.Subject.PrincipalId, cancellationToken)
            .ConfigureAwait(false);

        var envId = context.Resource.EnvironmentId;
        var applicableBindings = bindings
            .Where(b => b.EnvironmentId is null || b.EnvironmentId == envId)
            .Select(b => b.RoleName)
            .Concat(context.Subject.Roles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var expandedRoles = await _hierarchy
            .ExpandAsync(projectId.Value, applicableBindings, cancellationToken)
            .ConfigureAwait(false);

        // Rebuild subject with expanded roles for condition evaluation.
        var enriched = new AccessContext
        {
            Subject = new AccessSubject
            {
                PrincipalId = context.Subject.PrincipalId,
                Roles = expandedRoles.ToList(),
                Attributes = MergeAttrs(context.Subject.Attributes, new Dictionary<string, object?>
                {
                    ["roles"] = expandedRoles.ToList(),
                    ["sub"] = context.Subject.PrincipalId
                })
            },
            Resource = context.Resource,
            Action = context.Action,
            Environment = context.Environment
        };

        var policies = await _policies
            .ListPublishedAsync(projectId.Value, envId, cancellationToken)
            .ConfigureAwait(false);

        var matching = policies
            .Where(p => ActionMatches(p, enriched.Action))
            .Where(p => p.Resources.Any(r => ResourcePatternMatcher.Matches(r, enriched.Resource.Type, enriched.Resource.FieldPath)))
            .Where(p => ConditionEvaluator.Evaluate(p.Condition, enriched))
            .OrderByDescending(p => p.Priority)
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .ToList();

        // 1) Explicit hard Deny (entity-level or non-mask field deny) wins.
        var hardDenies = matching
            .Where(p => p.Effect == AccessEffect.Deny && !(p.FieldMaskOnly && p.Resources.Any(ResourcePatternMatcher.IsFieldScoped)))
            .ToList();

        if (hardDenies.Count > 0)
        {
            return AccessDecision.Deny(
                $"Denied by policy '{hardDenies[0].Name}'.",
                hardDenies.Select(p => p.PolicyId.ToString("N")).ToArray());
        }

        // Field-mask denies become obligations (applied only if Allow).
        var fieldMaskPolicies = matching
            .Where(p => p.Effect == AccessEffect.Deny && p.FieldMaskOnly)
            .ToList();

        var allows = matching.Where(p => p.Effect == AccessEffect.Allow).ToList();

        // RBAC gate: without any Allow policy, still allow if a built-in role implies the action
        // AND at least one role binding/role exists — but default deny unless RBAC capability matches.
        // MVP: Allow policies are required for data actions unless a role-capability Allow is present.
        if (allows.Count == 0)
        {
            // Built-in role capability fallback (acts as implicit Allow policies).
            if (RoleImpliesAction(expandedRoles, enriched.Action))
            {
                var obligations = BuildObligations(fieldMaskPolicies, allows, enriched);
                return AccessDecision.Allow(
                    $"Allowed by RBAC role(s): {string.Join(',', expandedRoles)}.",
                    obligations);
            }

            return AccessDecision.Deny("Default deny: no matching Allow policy and role does not imply action.");
        }

        var merged = BuildObligations(fieldMaskPolicies, allows, enriched);
        return AccessDecision.Allow(
            $"Allowed by policy '{allows[0].Name}'.",
            merged,
            allows.Select(p => p.PolicyId.ToString("N")).ToArray());
    }

    private static AccessObligations BuildObligations(
        IReadOnlyList<PolicyDocument> fieldMaskPolicies,
        IReadOnlyList<PolicyDocument> allows,
        AccessContext context)
    {
        var deniedRead = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deniedWrite = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rowFilters = new List<RowFilterObligation>();

        foreach (var policy in fieldMaskPolicies)
        {
            foreach (var resource in policy.Resources.Where(ResourcePatternMatcher.IsFieldScoped))
            {
                var field = ResourcePatternMatcher.ExtractFieldPath(resource);
                if (string.IsNullOrWhiteSpace(field))
                {
                    continue;
                }

                if (ActionMatches(policy, AccessActions.Read) || ActionMatches(policy, AccessActions.List))
                {
                    deniedRead.Add(field);
                }

                if (ActionMatches(policy, AccessActions.Create) || ActionMatches(policy, AccessActions.Update))
                {
                    deniedWrite.Add(field);
                }
            }
        }

        foreach (var policy in allows.Where(p => p.EmitRowFilterObligation && p.Condition is not null))
        {
            if (TryExtractEqualityRowFilter(policy.Condition!, out var filter))
            {
                rowFilters.Add(filter);
            }
        }

        _ = context;
        return new AccessObligations
        {
            DeniedReadFields = deniedRead,
            DeniedWriteFields = deniedWrite,
            RowFilters = rowFilters
        };
    }

    public static bool TryExtractEqualityRowFilter(ConditionExpression condition, out RowFilterObligation filter)
    {
        filter = null!;
        if (!string.Equals(condition.Op, "eq", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (condition.Left is not { IsAttribute: true, AttributePath: { } left }
            || condition.Right is not { IsAttribute: true, AttributePath: { } right })
        {
            return false;
        }

        string? resourceAttr = null;
        string? subjectAttr = null;

        if (left.StartsWith("resource.", StringComparison.OrdinalIgnoreCase))
        {
            resourceAttr = left["resource.".Length..];
        }
        else if (right.StartsWith("resource.", StringComparison.OrdinalIgnoreCase))
        {
            resourceAttr = right["resource.".Length..];
        }

        if (left.StartsWith("subject.", StringComparison.OrdinalIgnoreCase))
        {
            subjectAttr = left["subject.".Length..];
        }
        else if (right.StartsWith("subject.", StringComparison.OrdinalIgnoreCase))
        {
            subjectAttr = right["subject.".Length..];
        }

        if (resourceAttr is null || subjectAttr is null)
        {
            return false;
        }

        // Normalize owner aliases.
        if (resourceAttr.Equals("owner_id", StringComparison.OrdinalIgnoreCase)
            || resourceAttr.Equals("ownerid", StringComparison.OrdinalIgnoreCase))
        {
            resourceAttr = "created_by";
        }

        if (subjectAttr is "sub" or "id" or "principal_id" or "principalid")
        {
            subjectAttr = "sub";
        }

        filter = new RowFilterObligation(resourceAttr, subjectAttr);
        return true;
    }

    private static bool ActionMatches(PolicyDocument policy, string action) =>
        policy.Actions.Any(a => a == "*" || string.Equals(a, action, StringComparison.OrdinalIgnoreCase));

    private static bool RoleImpliesAction(IReadOnlySet<string> roles, string action)
    {
        // owner/editor: full data CRUD + file upload/download; viewer: read/list + download; authenticated: none by default.
        if (roles.Contains(BuiltInAppRoles.Owner) || roles.Contains(BuiltInAppRoles.Editor))
        {
            return action is AccessActions.Create or AccessActions.Read or AccessActions.Update
                or AccessActions.Delete or AccessActions.List
                or AccessActions.FileUpload or AccessActions.FileDownload
                or AccessActions.Subscribe or AccessActions.SqlExecute;
        }

        if (roles.Contains(BuiltInAppRoles.Viewer))
        {
            return action is AccessActions.Read or AccessActions.List or AccessActions.FileDownload
                or AccessActions.Subscribe or AccessActions.SqlExecute;
        }

        return false;
    }

    private static IReadOnlyDictionary<string, object?> MergeAttrs(
        IReadOnlyDictionary<string, object?> primary,
        IReadOnlyDictionary<string, object?> secondary)
    {
        var map = new Dictionary<string, object?>(secondary, StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in primary)
        {
            map[k] = v;
        }

        return map;
    }
}
