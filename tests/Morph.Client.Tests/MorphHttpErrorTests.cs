using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Morph.Client;
using Xunit;
using static Morph.Client.Tests.MorphClientTestHelpers;

namespace Morph.Client.Tests;

public sealed class MorphHttpErrorTests
{
    [Fact]
    public async Task Conflict_409_throws_MorphApiException_with_plain_body()
    {
        var handler = new StubHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent("version conflict", Encoding.UTF8, "text/plain")
            });

        using var client = CreateClient(handler);
        var ex = await Assert.ThrowsAsync<MorphApiException>(() => client.Data.CreateAsync("x", new { a = 1 }));
        ex.StatusCode.Should().Be(409);
        ex.ResponseBody.Should().Be("version conflict");
        ex.Message.Should().Contain("409");
    }

    [Fact]
    public async Task Internal_error_500_throws_MorphApiException()
    {
        var handler = new StubHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("<html>error</html>", Encoding.UTF8, "text/html")
            });

        using var client = CreateClient(handler);
        var ex = await Assert.ThrowsAsync<MorphApiException>(() => client.Tenancy.ListProjectsAsync());
        ex.StatusCode.Should().Be(500);
        ex.ResponseBody.Should().Contain("html");
    }

    [Fact]
    public async Task Non_json_error_body_is_preserved_on_failure()
    {
        var handler = new StubHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("not-json at all", Encoding.UTF8, "text/plain")
            });

        using var client = CreateClient(handler);
        var ex = await Assert.ThrowsAsync<MorphApiException>(() => client.Access.SimulateAsync(new { }));
        ex.StatusCode.Should().Be(400);
        ex.ResponseBody.Should().Be("not-json at all");
    }

    [Fact]
    public async Task Success_with_invalid_json_throws_on_deserialize()
    {
        var handler = new StubHandler((_, _) => JsonOk("<<<not json>>>"));

        using var client = CreateClient(handler);
        await Assert.ThrowsAsync<JsonException>(() => client.Auth.GetMeAsync());
    }

    [Fact]
    public async Task Login_with_null_token_json_throws_MorphApiException()
    {
        var handler = new StubHandler((req, _) =>
        {
            req.RequestUri!.AbsolutePath.Should().Be("/connect/token");
            return JsonOk("null");
        });

        using var client = CreateClient(handler);
        var ex = await Assert.ThrowsAsync<MorphApiException>(() => client.Auth.LoginAsync("a@b.c", "pw"));
        ex.StatusCode.Should().Be(200);
        ex.Message.Should().Contain("Invalid token response");
    }

    [Fact]
    public async Task Delete_with_empty_success_body_succeeds()
    {
        var id = Guid.NewGuid();
        var handler = new StubHandler((req, _) =>
        {
            req.Method.Should().Be(HttpMethod.Delete);
            return EmptyOk();
        });

        using var client = CreateClient(handler);
        await client.Data.DeleteAsync("employee", id);
    }
}
