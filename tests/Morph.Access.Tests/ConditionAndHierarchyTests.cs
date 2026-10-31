using FluentAssertions;
using Morph.Access.Domain;
using Morph.Access.Evaluation;
using Morph.Access.Persistence;
using Xunit;

namespace Morph.Access.Tests;

public sealed class ConditionAndHierarchyTests
{
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly InMemoryRoleStore _roles = new();

    public ConditionAndHierarchyTests()
    {
        _roles.EnsureBuiltInRolesAsync(_projectId, DateTimeOffset.UtcNow).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task RoleHierarchyExpander_owner_includes_editor_viewer_and_authenticated()
    {
        var expander = new RoleHierarchyExpander(_roles);
        var expanded = await expander.ExpandAsync(_projectId, [BuiltInAppRoles.Owner]);

        expanded.Should().Contain(BuiltInAppRoles.Owner);
        expanded.Should().Contain(BuiltInAppRoles.Editor);
        expanded.Should().Contain(BuiltInAppRoles.Viewer);
        expanded.Should().Contain(BuiltInAppRoles.Authenticated);
    }

    [Fact]
    public async Task RoleHierarchyExpander_empty_direct_roles_yields_empty_set()
    {
        var expander = new RoleHierarchyExpander(_roles);
        var expanded = await expander.ExpandAsync(_projectId, []);
        expanded.Should().BeEmpty();
    }

    [Fact]
    public void ConditionEvaluator_nested_and_requires_all_children()
    {
        var ctx = Context("alice", "editor", "task");
        var expr = ConditionExpression.And(
            ConditionExpression.EqLiteral("subject.sub", "alice"),
            ConditionExpression.EqLiteral("resource.type", "task"));

        ConditionEvaluator.Evaluate(expr, ctx).Should().BeTrue();
        ConditionEvaluator.Evaluate(expr, Context("bob", "editor", "task")).Should().BeFalse();
    }

    [Fact]
    public void ConditionEvaluator_nested_or_matches_any_child()
    {
        var ctx = Context("alice", "viewer", "doc");
        var expr = ConditionExpression.Or(
            ConditionExpression.EqLiteral("subject.sub", "bob"),
            ConditionExpression.EqLiteral("resource.type", "doc"));

        ConditionEvaluator.Evaluate(expr, ctx).Should().BeTrue();
    }

    [Fact]
    public void ConditionEvaluator_not_inverts_child()
    {
        var ctx = Context("alice", "viewer", "doc");
        var expr = ConditionExpression.Not(ConditionExpression.EqLiteral("subject.sub", "alice"));
        ConditionEvaluator.Evaluate(expr, ctx).Should().BeFalse();
    }

    [Fact]
    public async Task AccessEvaluator_denies_when_resource_project_id_missing()
    {
        var evaluator = new AccessEvaluator(_roles, new InMemoryPolicyStore(), new RoleHierarchyExpander(_roles));
        var decision = await evaluator.EvaluateAsync(new AccessContext
        {
            Subject = new AccessSubject { PrincipalId = "user-1", Roles = [BuiltInAppRoles.Owner] },
            Action = AccessActions.Read,
            Resource = new AccessResource { Type = "task", ProjectId = null }
        });

        decision.IsDenied.Should().BeTrue();
        decision.Reason.Should().Contain("ProjectId");
    }

    [Fact]
    public async Task AccessEvaluator_empty_subject_roles_without_bindings_default_deny()
    {
        var evaluator = new AccessEvaluator(_roles, new InMemoryPolicyStore(), new RoleHierarchyExpander(_roles));
        var decision = await evaluator.EvaluateAsync(new AccessContext
        {
            Subject = new AccessSubject { PrincipalId = "lonely-user", Roles = [] },
            Action = AccessActions.Read,
            Resource = new AccessResource { Type = "task", ProjectId = _projectId }
        });

        decision.IsDenied.Should().BeTrue();
        decision.Reason.Should().Contain("Default deny");
    }

    private static AccessContext Context(string principalId, string role, string entity) =>
        new()
        {
            Subject = new AccessSubject
            {
                PrincipalId = principalId,
                Roles = [role]
            },
            Action = AccessActions.Read,
            Resource = new AccessResource
            {
                Type = entity,
                ProjectId = Guid.NewGuid(),
                Attributes = new Dictionary<string, object?>()
            }
        };
}
