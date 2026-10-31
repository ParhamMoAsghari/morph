using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Morph.Host.Integration.Tests.Fixtures;
using Xunit;

namespace Morph.Host.Integration.Tests;

[Collection("morph-host")]
public sealed class HostPluginsAndOpenApiTests(MorphHostFixture fixture)
{
    [Fact]
    public async Task Plugins_catalog_and_unknown_route()
    {
        using var client = fixture.CreateClient();

        var catalog = await MorphApiHelper.GetJsonAsync(client, "/plugins/v1");
        catalog.GetProperty("plugins").ValueKind.Should().Be(JsonValueKind.Array);

        var unknown = await client.GetAsync("/plugins/v1/morph.missing-plugin/any-route");
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OpenApi_document_is_available_in_development()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("openapi").GetString().Should().NotBeNullOrWhiteSpace();
        json.GetProperty("paths").ValueKind.Should().Be(JsonValueKind.Object);
    }
}
