using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Morph.Host.Integration.Tests.Fixtures;
using Xunit;

namespace Morph.Host.Integration.Tests;

[Collection("morph-host")]
public sealed class PlatformProcessFunctionSmsTests(MorphHostFixture fixture)
{
    [Fact]
    public async Task Process_upsert_validate_publish_run_and_logs()
    {
        var session = await MorphApiHelper.BootstrapAsync(fixture);
        using var client = session.CreateClient(withEnv: false);

        const string yaml = """
            apiVersion: morph/v1
            kind: Process
            slug: greet
            displayName: Greet
            steps:
              - id: set
                type: variables.set
                with:
                  name: message
                  value: ok
            """;

        var upserted = await MorphApiHelper.PostJsonAsync(
            client,
            $"/v1/environments/{session.EnvironmentId}/processes",
            new { yaml });
        var definitionId = upserted.GetProperty("definitionId").GetGuid();
        var versionId = upserted.GetProperty("versionId").GetGuid();

        (await client.PostAsync(
            $"/v1/environments/{session.EnvironmentId}/processes/versions/{versionId}/validate",
            null)).EnsureSuccessStatusCode();

        var authoring = await MorphApiHelper.GetJsonAsync(
            client,
            $"/v1/environments/{session.EnvironmentId}/processes/versions/{versionId}/authoring");
        authoring.ValueKind.Should().NotBe(JsonValueKind.Undefined);

        (await client.PostAsync(
            $"/v1/environments/{session.EnvironmentId}/processes/versions/{versionId}/publish",
            null)).EnsureSuccessStatusCode();

        var listed = await MorphApiHelper.GetJsonAsync(
            client,
            $"/v1/environments/{session.EnvironmentId}/processes");
        listed.EnumerateArray().Any(p => p.GetProperty("definitionId").GetGuid() == definitionId).Should().BeTrue();

        var def = await MorphApiHelper.GetJsonAsync(
            client,
            $"/v1/environments/{session.EnvironmentId}/processes/{definitionId}");
        def.GetProperty("slug").GetString().Should().Be("greet");

        var version = await MorphApiHelper.GetJsonAsync(
            client,
            $"/v1/environments/{session.EnvironmentId}/processes/versions/{versionId}");
        version.GetProperty("versionId").GetGuid().Should().Be(versionId);

        var run = await MorphApiHelper.PostJsonAsync(
            client,
            $"/v1/environments/{session.EnvironmentId}/processes/greet/run",
            new { input = new { hello = "world" } });
        var instanceId = run.GetProperty("instanceId").GetGuid();
        // Pure variables.set process must finish synchronously.
        run.GetProperty("status").GetString().Should().Be("Completed");

        var instance = await MorphApiHelper.GetJsonAsync(
            client,
            $"/v1/environments/{session.EnvironmentId}/processes/instances/{instanceId}");
        instance.GetProperty("status").GetString().Should().Be("Completed");

        var logs = await MorphApiHelper.GetJsonAsync(
            client,
            $"/v1/environments/{session.EnvironmentId}/processes/instances/{instanceId}/logs");
        logs.ValueKind.Should().Be(JsonValueKind.Array);
        logs.GetArrayLength().Should().BeGreaterThan(0);

        // push alias
        var pushed = await MorphApiHelper.PostJsonAsync(
            client,
            $"/v1/environments/{session.EnvironmentId}/processes/push",
            new { yaml });
        pushed.GetProperty("definitionId").GetGuid().Should().Be(definitionId);
    }

    [Fact]
    public async Task Process_with_delay_step_enters_waiting_status()
    {
        var session = await MorphApiHelper.BootstrapAsync(fixture);
        using var client = session.CreateClient(withEnv: false);

        const string yaml = """
            apiVersion: morph/v1
            kind: Process
            slug: pause
            displayName: Pause
            steps:
              - id: wait
                type: delay
                with:
                  seconds: 120
            """;

        var upserted = await MorphApiHelper.PostJsonAsync(
            client,
            $"/v1/environments/{session.EnvironmentId}/processes",
            new { yaml });
        var versionId = upserted.GetProperty("versionId").GetGuid();

        (await client.PostAsync(
            $"/v1/environments/{session.EnvironmentId}/processes/versions/{versionId}/validate",
            null)).EnsureSuccessStatusCode();

        (await client.PostAsync(
            $"/v1/environments/{session.EnvironmentId}/processes/versions/{versionId}/publish",
            null)).EnsureSuccessStatusCode();

        var run = await MorphApiHelper.PostJsonAsync(
            client,
            $"/v1/environments/{session.EnvironmentId}/processes/pause/run",
            new { input = new { } });
        run.GetProperty("status").GetString().Should().Be("Waiting");
        run.GetProperty("instanceId").GetGuid().Should().NotBeEmpty();
    }

    [Fact]
    public async Task Functions_register_list_and_invoke()
    {
        var session = await MorphApiHelper.BootstrapAsync(fixture);
        using var client = session.CreateClient(withEnv: false);

        var registered = await MorphApiHelper.PostJsonAsync(
            client,
            $"/v1/environments/{session.EnvironmentId}/functions",
            new
            {
                slug = "echo",
                displayName = "Echo",
                runtime = "TypeScriptStub",
                trigger = "Http",
                source = "export default async (ctx) => ctx.input"
            });
        registered.GetProperty("slug").GetString().Should().Be("echo");

        var listed = await MorphApiHelper.GetJsonAsync(
            client,
            $"/v1/environments/{session.EnvironmentId}/functions");
        listed.EnumerateArray().Any(f => f.GetProperty("slug").GetString() == "echo").Should().BeTrue();

        var invoked = await MorphApiHelper.PostJsonAsync(
            client,
            $"/v1/environments/{session.EnvironmentId}/functions/echo/invoke",
            new { input = new { ping = true } });
        invoked.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task Sms_send_validates_and_delivers_via_configured_provider()
    {
        var session = await MorphApiHelper.BootstrapAsync(fixture);
        using var client = session.CreateClient(withEnv: false);

        var (invalid, _) = await MorphApiHelper.SendAsync(
            client,
            HttpMethod.Post,
            "/v1/sms/send",
            new { to = "", body = "hello morph" });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var sent = await MorphApiHelper.PostJsonAsync(
            client,
            "/v1/sms/send",
            new { to = "+15555550100", body = "hello morph" });
        sent.GetProperty("succeeded").GetBoolean().Should().BeTrue();
        sent.GetProperty("providerMessageId").GetString().Should().StartWith("test-");
    }
}
