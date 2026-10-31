using System.Net;
using System.Text;
using FluentAssertions;
using Morph.Client;
using Morph.Client.Models;
using Xunit;

namespace Morph.Client.Tests;

public sealed class MorphClientTenancyDefinitionsTests
{
    [Fact]
    public async Task Tenancy_create_organization_posts_json_body()
    {
        var handler = new StubHandler((req, _) =>
        {
            req.Method.Should().Be(HttpMethod.Post);
            req.RequestUri!.AbsolutePath.Should().Be("/v1/orgs");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"id":"11111111-1111-1111-1111-111111111111","name":"Acme","slug":"acme"}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://morph.test/") };
        using var client = new MorphClient(new MorphClientOptions
        {
            BaseAddress = new Uri("http://morph.test/"),
            AccessToken = "tok"
        }, http);

        var org = await client.Tenancy.CreateOrganizationAsync(new CreateOrganizationRequest("Acme", "acme"));
        org.Slug.Should().Be("acme");
    }

    [Fact]
    public async Task Definitions_create_posts_to_environment_path()
    {
        var envId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var handler = new StubHandler((req, _) =>
        {
            req.Method.Should().Be(HttpMethod.Post);
            req.RequestUri!.AbsolutePath.Should().Be($"/v1/environments/{envId:D}/definitions");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"definitionId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","versionId":"cccccccc-cccc-cccc-cccc-cccccccccccc","slug":"task","kind":"Entity","status":"Draft"}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://morph.test/") };
        using var client = new MorphClient(new MorphClientOptions
        {
            BaseAddress = new Uri("http://morph.test/"),
            AccessToken = "tok"
        }, http);

        var created = await client.Definitions.CreateAsync(
            envId,
            new CreateDefinitionRequest("task", "Task", "Entity", Fields: null));

        created.Slug.Should().Be("task");
        created.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task Forbidden_response_maps_to_MorphApiException_with_body()
    {
        var handler = new StubHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("""{"error":"forbidden"}""")
            });

        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://morph.test/") };
        using var client = new MorphClient(new MorphClientOptions
        {
            BaseAddress = new Uri("http://morph.test/")
        }, http);

        var act = async () => await client.Tenancy.ListOrganizationsAsync();
        var ex = await act.Should().ThrowAsync<MorphApiException>();
        ex.Which.StatusCode.Should().Be(403);
        ex.Which.ResponseBody.Should().Contain("forbidden");
    }

    [Fact]
    public async Task Not_found_response_maps_to_MorphApiException_with_body()
    {
        var envId = Guid.NewGuid();
        var handler = new StubHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("missing definition")
            });

        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://morph.test/") };
        using var client = new MorphClient(new MorphClientOptions
        {
            BaseAddress = new Uri("http://morph.test/")
        }, http);

        var act = async () => await client.Definitions.ListAsync(envId);
        var ex = await act.Should().ThrowAsync<MorphApiException>();
        ex.Which.StatusCode.Should().Be(404);
        ex.Which.ResponseBody.Should().Be("missing definition");
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _handler;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler) =>
            _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request, cancellationToken));
    }
}
