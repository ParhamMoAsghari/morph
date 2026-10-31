using System.Net;
using System.Text;
using FluentAssertions;
using Morph.Host.Integration.Tests.Fixtures;
using Xunit;

namespace Morph.Host.Integration.Tests;

[Collection("morph-host")]
public sealed class HostSmokeTests(MorphHostFixture fixture)
{
    [Fact]
    public async Task Health_ready_is_healthy()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/health/ready");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_live_is_healthy()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/health/live");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Data_api_list_requires_environment_id()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/data/v1/post");
        // Missing X-Morph-Env-Id / envId is a client error, not an auth challenge.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unauthenticated_data_mutation_is_rejected()
    {
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "X-Morph-Env-Id",
            Guid.NewGuid().ToString("D"));

        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/data/v1/post", content);

        // Writes use RequireAuthorization; reads may proceed as Access "anon".
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
