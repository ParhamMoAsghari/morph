using FluentAssertions;
using Morph.Access.Domain;
using Morph.Access.Evaluation;
using Morph.Access.Persistence;
using Xunit;

namespace Morph.Access.Tests;

public sealed class AccessEvaluatorTests
{
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly InMemoryRoleStore _roles = new();
    private readonly InMemoryPolicyStore _policies = new();
    private readonly AccessEvaluator _evaluator;

    public AccessEvaluatorTests()
    {
        _roles.EnsureBuiltInRolesAsync(_projectId, DateTimeOffset.UtcNow).GetAwaiter().GetResult();
        _evaluator = new AccessEvaluator(_roles, _policies, new RoleHierarchyExpander(_roles));
    }

    [Fact]
    public async Task Default_deny_when_no_roles_and_no_policies()
    {
        var decision = await _evaluator.EvaluateAsync(Context("anon-user", "task", AccessActions.Read));
        decision.IsDenied.Should().BeTrue();
        decision.Reason.Should().Contain("Default deny");
    }

    [Fact]
    public async Task Rbac_editor_allows_create()
    {
        await Bind("user-1", BuiltInAppRoles.Editor);
        var decision = await _evaluator.EvaluateAsync(Context("user-1", "task", AccessActions.Create));
        decision.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task Rbac_viewer_denies_create_allows_read()
    {
        await Bind("user-2", BuiltInAppRoles.Viewer);

        var create = await _evaluator.EvaluateAsync(Context("user-2", "task", AccessActions.Create));
        create.IsDenied.Should().BeTrue();

        var read = await _evaluator.EvaluateAsync(Context("user-2", "task", AccessActions.Read));
        read.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task Owner_inherits_editor_and_viewer()
    {
        await Bind("owner-1", BuiltInAppRoles.Owner);
        var decision = await _evaluator.EvaluateAsync(Context("owner-1", "task", AccessActions.Delete));
        decision.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task Explicit_pbac_deny_wins_over_role_allow()
    {
        await Bind("user-3", BuiltInAppRoles.Editor);
        await Publish(new PolicyDocument
        {
            PolicyId = Guid.NewGuid(),
            ProjectId = _projectId,
            Name = "deny-delete-task",
            Effect = AccessEffect.Deny,
            Actions = [AccessActions.Delete],
            Resources = ["entity:task"],
            Priority = 100,
            Status = PolicyDocumentStatus.Published,
            CreatedAt = DateTimeOffset.UtcNow,
            PublishedAt = DateTimeOffset.UtcNow
        });

        var decision = await _evaluator.EvaluateAsync(Context("user-3", "task", AccessActions.Delete));
        decision.IsDenied.Should().BeTrue();
        decision.Reason.Should().Contain("deny-delete-task");
    }

    [Fact]
    public async Task Abac_owner_condition_allows_only_owner()
    {
        await Bind("alice", BuiltInAppRoles.Viewer);
        await Publish(new PolicyDocument
        {
            PolicyId = Guid.NewGuid(),
            ProjectId = _projectId,
            Name = "owner-read",
            Effect = AccessEffect.Allow,
            Actions = [AccessActions.Read],
            Resources = ["entity:doc"],
            Condition = ConditionExpression.Eq("resource.owner_id", "subject.sub"),
            EmitRowFilterObligation = true,
            Priority = 10,
            Status = PolicyDocumentStatus.Published,
            CreatedAt = DateTimeOffset.UtcNow,
            PublishedAt = DateTimeOffset.UtcNow
        });

        // Pure ABAC: authenticated only (no viewer RBAC read).
        await Bind("bob", BuiltInAppRoles.Authenticated);

        var aliceId = Guid.NewGuid().ToString("N");
        await Bind(aliceId, BuiltInAppRoles.Authenticated);

        var allow = await _evaluator.EvaluateAsync(Context(
            aliceId,
            "doc",
            AccessActions.Read,
            resourceAttrs: new Dictionary<string, object?> { ["owner_id"] = aliceId }));
        allow.IsAllowed.Should().BeTrue();
        allow.Obligations.RowFilters.Should().NotBeEmpty();

        var deny = await _evaluator.EvaluateAsync(Context(
            aliceId,
            "doc",
            AccessActions.Read,
            resourceAttrs: new Dictionary<string, object?> { ["owner_id"] = Guid.NewGuid().ToString("N") }));
        deny.IsDenied.Should().BeTrue();
    }

    [Fact]
    public async Task Pbac_field_mask_deny_for_viewer_becomes_obligation()
    {
        await Bind("viewer-1", BuiltInAppRoles.Viewer);
        await Publish(new PolicyDocument
        {
            PolicyId = Guid.NewGuid(),
            ProjectId = _projectId,
            Name = "hide-salary",
            Effect = AccessEffect.Deny,
            Actions = [AccessActions.Read, AccessActions.List],
            Resources = ["entity:employee/fields:salary"],
            FieldMaskOnly = true,
            Condition = ConditionExpression.And(
                new ConditionExpression
                {
                    Op = "in",
                    Left = ConditionOperand.Literal("viewer"),
                    Right = ConditionOperand.Attr("subject.roles")
                },
                ConditionExpression.Not(new ConditionExpression
                {
                    Op = "in",
                    Left = ConditionOperand.Literal("editor"),
                    Right = ConditionOperand.Attr("subject.roles")
                })),
            Priority = 50,
            Status = PolicyDocumentStatus.Published,
            CreatedAt = DateTimeOffset.UtcNow,
            PublishedAt = DateTimeOffset.UtcNow
        });

        var decision = await _evaluator.EvaluateAsync(Context("viewer-1", "employee", AccessActions.Read));
        decision.IsAllowed.Should().BeTrue();
        decision.Obligations.DeniedReadFields.Should().Contain("salary");

        await Bind("editor-1", BuiltInAppRoles.Editor);
        var editorDecision = await _evaluator.EvaluateAsync(Context("editor-1", "employee", AccessActions.Read));
        editorDecision.IsAllowed.Should().BeTrue();
        editorDecision.Obligations.DeniedReadFields.Should().BeEmpty();
    }

    [Fact]
    public async Task Draft_policies_are_ignored()
    {
        await Bind("user-4", BuiltInAppRoles.Authenticated);
        await _policies.SaveAsync(new PolicyDocument
        {
            PolicyId = Guid.NewGuid(),
            ProjectId = _projectId,
            Name = "draft-allow",
            Effect = AccessEffect.Allow,
            Actions = [AccessActions.Read],
            Resources = ["entity:task"],
            Status = PolicyDocumentStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var decision = await _evaluator.EvaluateAsync(Context("user-4", "task", AccessActions.Read));
        decision.IsDenied.Should().BeTrue();
    }

    [Fact]
    public async Task Anonymous_principal_allowed_by_public_read_policy_denied_without()
    {
        await Publish(new PolicyDocument
        {
            PolicyId = Guid.NewGuid(),
            ProjectId = _projectId,
            Name = "public.read",
            Effect = AccessEffect.Allow,
            Actions = [AccessActions.Read, AccessActions.List],
            Resources = ["entity:post"],
            Priority = 10,
            Status = PolicyDocumentStatus.Published,
            CreatedAt = DateTimeOffset.UtcNow,
            PublishedAt = DateTimeOffset.UtcNow
        });

        var anonPost = await _evaluator.EvaluateAsync(new AccessContext
        {
            Subject = new AccessSubject
            {
                PrincipalId = "anonymous",
                Roles = [BuiltInAppRoles.Anon]
            },
            Action = AccessActions.List,
            Resource = new AccessResource
            {
                Type = "post",
                ProjectId = _projectId
            }
        });
        anonPost.IsAllowed.Should().BeTrue();

        var anonPrivate = await _evaluator.EvaluateAsync(new AccessContext
        {
            Subject = new AccessSubject
            {
                PrincipalId = "anonymous",
                Roles = [BuiltInAppRoles.Anon]
            },
            Action = AccessActions.List,
            Resource = new AccessResource
            {
                Type = "author",
                ProjectId = _projectId
            }
        });
        anonPrivate.IsDenied.Should().BeTrue();
    }

    [Fact]
    public void Condition_eq_extracts_row_filter_obligation()
    {
        var ok = AccessEvaluator.TryExtractEqualityRowFilter(
            ConditionExpression.Eq("resource.owner_id", "subject.sub"),
            out var filter);

        ok.Should().BeTrue();
        filter.ResourceAttribute.Should().Be("created_by");
        filter.SubjectAttribute.Should().Be("sub");
    }

    private AccessContext Context(
        string principalId,
        string entity,
        string action,
        IReadOnlyDictionary<string, object?>? resourceAttrs = null) =>
        new()
        {
            Subject = new AccessSubject { PrincipalId = principalId },
            Action = action,
            Resource = new AccessResource
            {
                Type = entity,
                ProjectId = _projectId,
                Attributes = resourceAttrs ?? new Dictionary<string, object?>()
            }
        };

    private Task Bind(string principalId, string role) =>
        _roles.SaveBindingAsync(new RoleBinding
        {
            BindingId = Guid.NewGuid(),
            ProjectId = _projectId,
            PrincipalId = principalId,
            RoleName = role,
            CreatedAt = DateTimeOffset.UtcNow
        });

    private Task Publish(PolicyDocument policy) => _policies.SaveAsync(policy);
}
