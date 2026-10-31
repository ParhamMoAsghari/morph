using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Morph.Host.Integration.Tests.Fixtures;
using Xunit;

namespace Morph.Host.Integration.Tests;

[Collection("morph-host")]
public sealed class PlatformAuthAndTenancyTests(MorphHostFixture fixture)
{
    [Fact]
    public async Task Register_token_me_and_tenancy_lists_succeed()
    {
        var session = await MorphApiHelper.BootstrapAsync(fixture);
        using var client = session.CreateClient(withEnv: false);

        var me = await MorphApiHelper.GetJsonAsync(client, "/v1/auth/me");
        me.GetProperty("email").GetString().Should().Be(session.Email);

        var orgs = await MorphApiHelper.GetJsonAsync(client, "/v1/orgs");
        orgs.GetArrayLength().Should().BeGreaterThan(0);

        var projects = await MorphApiHelper.GetJsonAsync(client, "/v1/projects");
        projects.GetArrayLength().Should().BeGreaterThan(0);

        var envs = await MorphApiHelper.GetJsonAsync(client, $"/v1/projects/{session.ProjectId}/environments");
        envs.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Forgot_password_returns_accepted_without_leaking_existence()
    {
        using var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/v1/auth/forgot-password", new
        {
            email = $"missing-{Guid.NewGuid():N}@morph.test"
        });
        // Always 202 so callers cannot probe whether the email exists.
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Duplicate_org_slug_conflicts()
    {
        var session = await MorphApiHelper.BootstrapAsync(fixture);
        using var client = session.CreateClient(withEnv: false);

        var slug = $"dup-{Guid.NewGuid():N}"[..16];
        (await MorphApiHelper.PostJsonAsync(client, "/v1/orgs", new { name = "A", slug })).Should().NotBeNull();

        var (response, _) = await MorphApiHelper.SendAsync(
            client,
            HttpMethod.Post,
            "/v1/orgs",
            new { name = "B", slug });
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Reset_password_with_invalid_token_fails_validation()
    {
        using var client = fixture.CreateClient();
        var (response, body) = await MorphApiHelper.SendAsync(
            client,
            HttpMethod.Post,
            "/v1/auth/reset-password",
            new { userId = Guid.NewGuid(), token = "not-a-token", newPassword = "Password1!" });
        // Unknown user / bad token → validation problem (400), never 500.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("userId");
    }

    [Fact]
    public async Task Reset_password_with_invalid_token_encoding_fails_validation()
    {
        var session = await MorphApiHelper.BootstrapAsync(fixture);
        using var client = fixture.CreateClient();
        var (response, body) = await MorphApiHelper.SendAsync(
            client,
            HttpMethod.Post,
            "/v1/auth/reset-password",
            new { userId = session.UserId, token = "###", newPassword = "Password1!" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("token", because: "validation should mention token");
    }

    [Fact]
    public async Task Verify_email_unknown_user_fails_validation()
    {
        using var client = fixture.CreateClient();
        var (response, body) = await MorphApiHelper.SendAsync(
            client,
            HttpMethod.Post,
            "/v1/auth/verify-email",
            new { userId = Guid.NewGuid(), token = "dG9rZW4" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("userId", because: "validation should mention userId");
    }

    [Fact]
    public async Task Verify_email_invalid_token_encoding_fails_validation()
    {
        var session = await MorphApiHelper.BootstrapAsync(fixture);
        using var client = fixture.CreateClient();
        var (response, body) = await MorphApiHelper.SendAsync(
            client,
            HttpMethod.Post,
            "/v1/auth/verify-email",
            new { userId = session.UserId, token = "%%%not-base64url%%%" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("token", because: "validation should mention token");
    }

    [Fact]
    public async Task Verify_email_wrong_token_fails_validation()
    {
        var session = await MorphApiHelper.BootstrapAsync(fixture);
        using var client = fixture.CreateClient();
        var (response, body) = await MorphApiHelper.SendAsync(
            client,
            HttpMethod.Post,
            "/v1/auth/verify-email",
            new { userId = session.UserId, token = "d3Jvbmd0b2tlbg" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().NotBeNullOrWhiteSpace();
    }
}
