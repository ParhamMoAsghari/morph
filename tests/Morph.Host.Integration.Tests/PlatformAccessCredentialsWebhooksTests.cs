using System.Net;
using System.Text.Json;
using FluentAssertions;
using Morph.Host.Integration.Tests.Fixtures;
using Xunit;

namespace Morph.Host.Integration.Tests;

[Collection("morph-host")]
public sealed class PlatformAccessCredentialsWebhooksTests(MorphHostFixture fixture)
{
    [Fact]
    public async Task Roles_policies_and_bindings_endpoints_work()
    {
        var session = await MorphApiHelper.BootstrapAsync(fixture);
        using var client = session.CreateClient(withEnv: false);

        var roles = await MorphApiHelper.GetJsonAsync(client, $"/v1/projects/{session.ProjectId}/roles");
        roles.EnumerateArray().Select(r => r.GetProperty("name").GetString()).Should().Contain("owner");

        var createdRole = await MorphApiHelper.PostJsonAsync(
            client,
            $"/v1/projects/{session.ProjectId}/roles",
            new { name = "analyst", displayName = "Analyst", inherits = new[] { "viewer" } });
        createdRole.GetProperty("roleId").GetGuid().Should().NotBeEmpty();

        var bindings = await MorphApiHelper.GetJsonAsync(
            client,
            $"/v1/projects/{session.ProjectId}/role-bindings");
        bindings.GetArrayLength().Should().BeGreaterThan(0);

        var policy = await MorphApiHelper.PostJsonAsync(
            client,
            $"/v1/projects/{session.ProjectId}/policies",
            new
            {
                name = $"allow-read-{Guid.NewGuid():N}"[..20],
                effect = "Allow",
                actions = new[] { "read", "list" },
                resources = new[] { "entity:note" },
                priority = 10,
                environmentId = session.EnvironmentId
            });
        var policyId = policy.GetProperty("policyId").GetGuid();

        (await client.PostAsync(
            $"/v1/projects/{session.ProjectId}/policies/{policyId}/publish",
            null)).EnsureSuccessStatusCode();

        var policies = await MorphApiHelper.GetJsonAsync(
            client,
            $"/v1/projects/{session.ProjectId}/policies");
        policies.EnumerateArray().Any(p => p.GetProperty("policyId").GetGuid() == policyId).Should().BeTrue();
    }

    [Fact]
    public async Task Credentials_issue_list_and_revoke()
    {
        var session = await MorphApiHelper.BootstrapAsync(fixture);
        using var client = session.CreateClient(withEnv: false);

        var issued = await MorphApiHelper.PostJsonAsync(
            client,
            $"/v1/projects/{session.ProjectId}/credentials",
            new { name = "ci-key", environmentId = session.EnvironmentId });
        var credentialId = issued.GetProperty("id").GetGuid();
        issued.TryGetProperty("secret", out _).Should().BeTrue();

        var listed = await MorphApiHelper.GetJsonAsync(
            client,
            $"/v1/projects/{session.ProjectId}/credentials");
        listed.EnumerateArray().Any(c => c.GetProperty("id").GetGuid() == credentialId).Should().BeTrue();

        (await client.PostAsync(
            $"/v1/projects/{session.ProjectId}/credentials/{credentialId}/revoke",
            null)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Webhooks_create_list_and_delete()
    {
        var session = await MorphApiHelper.BootstrapAsync(fixture);
        using var client = session.CreateClient(withEnv: false);

        var created = await MorphApiHelper.PostJsonAsync(
            client,
            $"/v1/projects/{session.ProjectId}/webhooks",
            new
            {
                targetUrl = "https://example.com/morph-hook",
                environmentId = session.EnvironmentId,
                eventTypes = new[] { "record.created", "record.updated" }
            });
        var subscriptionId = created.TryGetProperty("id", out var idEl)
            ? idEl.GetGuid()
            : created.GetProperty("subscriptionId").GetGuid();

        var listed = await MorphApiHelper.GetJsonAsync(
            client,
            $"/v1/projects/{session.ProjectId}/webhooks");
        listed.GetArrayLength().Should().BeGreaterThan(0);

        var deleted = await client.DeleteAsync(
            $"/v1/projects/{session.ProjectId}/webhooks/{subscriptionId}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Webhook_with_private_url_is_rejected()
    {
        var session = await MorphApiHelper.BootstrapAsync(fixture);
        using var client = session.CreateClient(withEnv: false);

        var (response, body) = await MorphApiHelper.SendAsync(
            client,
            HttpMethod.Post,
            $"/v1/projects/{session.ProjectId}/webhooks",
            new
            {
                targetUrl = "http://127.0.0.1/hook",
                environmentId = session.EnvironmentId,
                eventTypes = new[] { "record.created" }
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("targetUrl");
    }
}
