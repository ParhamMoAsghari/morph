using System.Net;
using System.Text;
using FluentAssertions;
using Morph.Client;
using Xunit;

namespace Morph.Client.Tests;

public sealed class MorphClientHttpTests
{
    [Fact]
    public async Task Data_list_sends_env_and_auth_headers()
    {
        var handler = new StubHandler((req, _) =>
        {
            req.Headers.Authorization!.Parameter.Should().Be("tok");
            req.Headers.GetValues("X-Morph-Env-Id").Single().Should().Be("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            req.RequestUri!.AbsolutePath.Should().Be("/data/v1/employee");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"items":[],"hasMore":false}""", Encoding.UTF8, "application/json")
            };
        });

        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://morph.test/") };
        using var client = new MorphClient(new MorphClientOptions
        {
            BaseAddress = new Uri("http://morph.test/"),
            AccessToken = "tok",
            EnvironmentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
        }, http);

        var page = await client.Data.ListAsync("employee");
        page.Items.Should().BeEmpty();
        page.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task Auth_login_posts_form_and_stores_token()
    {
        var handler = new StubHandler((req, _) =>
        {
            req.Method.Should().Be(HttpMethod.Post);
            req.RequestUri!.AbsolutePath.Should().Be("/connect/token");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"access_token":"abc","token_type":"Bearer","expires_in":3600}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://morph.test/") };
        using var client = new MorphClient(new MorphClientOptions
        {
            BaseAddress = new Uri("http://morph.test/")
        }, http);

        var token = await client.Auth.LoginAsync("a@b.c", "Password1!");
        token.AccessToken.Should().Be("abc");
        client.Options.AccessToken.Should().Be("abc");
    }

    [Fact]
    public async Task Failed_response_throws_MorphApiException()
    {
        var handler = new StubHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("nope")
            });

        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://morph.test/") };
        using var client = new MorphClient(new MorphClientOptions
        {
            BaseAddress = new Uri("http://morph.test/")
        }, http);

        var act = async () => await client.Auth.GetMeAsync();
        var ex = await act.Should().ThrowAsync<MorphApiException>();
        ex.Which.StatusCode.Should().Be(401);
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
