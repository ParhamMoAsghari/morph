using System.Net;
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
    public async Task Unauthenticated_data_api_is_rejected()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/data/v1/post");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
