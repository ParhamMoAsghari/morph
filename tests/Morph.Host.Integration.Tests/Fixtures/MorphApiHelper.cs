using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace Morph.Host.Integration.Tests.Fixtures;

public sealed class MorphSession
{
    public required MorphHostFixture Fixture { get; init; }
    public required string Email { get; init; }
    public required string AccessToken { get; init; }
    public required Guid UserId { get; init; }
    public required Guid OrganizationId { get; init; }
    public required Guid ProjectId { get; init; }
    public required Guid EnvironmentId { get; init; }

    public HttpClient CreateClient(bool withEnv = true)
    {
        var client = Fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", AccessToken);
        if (withEnv)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "X-Morph-Env-Id",
                EnvironmentId.ToString("D"));
        }

        return client;
    }
}

public static class MorphApiHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<MorphSession> BootstrapAsync(MorphHostFixture fixture, string? email = null)
    {
        using var client = fixture.CreateClient();
        email ??= $"user-{Guid.NewGuid():N}@morph.test";
        const string password = "Password1!";

        var register = await client.PostAsJsonAsync("/v1/auth/register", new
        {
            email,
            password,
            displayName = "E2E Tester"
        });
        register.EnsureSuccessStatusCode();

        using var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = email,
            ["password"] = password,
            ["client_id"] = "morph-platform",
            ["client_secret"] = "morph-platform-dev-secret",
            ["scope"] = "openid profile email offline_access morph.platform"
        });
        var tokenResponse = await client.PostAsync("/connect/token", tokenRequest);
        tokenResponse.EnsureSuccessStatusCode();
        var tokenPayload = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = tokenPayload.GetProperty("access_token").GetString()!;

        using var authed = CreateAuthed(fixture, accessToken);

        var me = await authed.GetFromJsonAsync<JsonElement>("/v1/auth/me");
        var userId = me.GetProperty("id").GetGuid();

        var stamp = Guid.NewGuid().ToString("N")[..10];
        var org = await PostJsonAsync(authed, "/v1/orgs", new { name = $"Org {stamp}", slug = $"org-{stamp}" });
        var orgId = org.GetProperty("id").GetGuid();

        var project = await PostJsonAsync(authed, "/v1/projects", new
        {
            organizationId = orgId,
            name = $"Project {stamp}",
            slug = $"proj-{stamp}"
        });
        var projectId = project.GetProperty("id").GetGuid();

        var env = await PostJsonAsync(authed, $"/v1/projects/{projectId}/environments", new
        {
            name = "Development",
            slug = "dev"
        });
        var environmentId = env.GetProperty("id").GetGuid();

        (await authed.GetAsync($"/v1/projects/{projectId}/roles")).EnsureSuccessStatusCode();
        var bind = await PostJsonAsync(authed, $"/v1/projects/{projectId}/role-bindings", new
        {
            principalId = userId.ToString("D"),
            roleName = "owner",
            environmentId
        });
        bind.GetProperty("bindingId").GetGuid().Should().NotBeEmpty();

        return new MorphSession
        {
            Fixture = fixture,
            Email = email,
            AccessToken = accessToken,
            UserId = userId,
            OrganizationId = orgId,
            ProjectId = projectId,
            EnvironmentId = environmentId
        };
    }

    public static HttpClient CreateAuthed(
        MorphHostFixture fixture,
        string accessToken,
        Guid? environmentId = null)
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        if (environmentId is not null)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "X-Morph-Env-Id",
                environmentId.Value.ToString("D"));
        }

        return client;
    }

    public static async Task<JsonElement> PostJsonAsync(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        var text = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"POST {url} => {(int)response.StatusCode}: {text}");
        return string.IsNullOrWhiteSpace(text)
            ? default
            : JsonSerializer.Deserialize<JsonElement>(text, JsonOptions);
    }

    public static async Task<JsonElement> GetJsonAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        var text = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"GET {url} => {(int)response.StatusCode}: {text}");
        return JsonSerializer.Deserialize<JsonElement>(text, JsonOptions);
    }

    public static async Task<(HttpResponseMessage Response, string Body)> SendAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");
        }

        var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        return (response, text);
    }

    public static async Task<(Guid DefinitionId, Guid VersionId)> CreatePublishMigrateEntityAsync(
        MorphSession session,
        string slug = "post",
        string displayName = "Post")
    {
        using var client = session.CreateClient(withEnv: false);
        var created = await PostJsonAsync(
            client,
            $"/v1/environments/{session.EnvironmentId}/definitions",
            new
            {
                slug,
                displayName,
                kind = "Entity",
                fields = new object[]
                {
                    new
                    {
                        fieldId = Guid.Empty,
                        slug = "title",
                        displayName = "Title",
                        dataType = "String",
                        isRequired = true,
                        isIndexed = true
                    },
                    new
                    {
                        fieldId = Guid.Empty,
                        slug = "body",
                        displayName = "Body",
                        dataType = "String"
                    }
                }
            });

        var definitionId = created.GetProperty("definitionId").GetGuid();
        var versionId = created.GetProperty("versionId").GetGuid();

        (await client.PostAsync(
            $"/v1/environments/{session.EnvironmentId}/definitions/versions/{versionId}/publish",
            null)).EnsureSuccessStatusCode();

        await PostJsonAsync(
            client,
            $"/v1/environments/{session.EnvironmentId}/migrations/plan",
            new { toVersionId = versionId });

        var applied = await PostJsonAsync(
            client,
            $"/v1/environments/{session.EnvironmentId}/migrations/apply",
            new { toVersionId = versionId });
        applied.GetProperty("status").GetString().Should().Be("Completed");

        return (definitionId, versionId);
    }
}
