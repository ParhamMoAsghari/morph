using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Morph.Host.Integration.Tests.Fixtures;
using Xunit;

namespace Morph.Host.Integration.Tests;

/// <summary>
/// End-to-end workflow mirroring the Dev Panel: bootstrap a real Community Blog project
/// and exercise every API surface the panel uses, with ideal contract assertions.
/// </summary>
[Collection("morph-host")]
public sealed class CommunityBlogPanelWorkflowTests(MorphHostFixture fixture)
{
    [Fact]
    public async Task Build_community_blog_through_panel_apis()
    {
        // --- Overview / Login: register, token, tenancy, owner bind ---
        var session = await MorphApiHelper.BootstrapAsync(fixture);
        using var platform = session.CreateClient(withEnv: false);
        using var data = session.CreateClient(withEnv: true);

        var health = await MorphApiHelper.GetJsonAsync(fixture.CreateClient(), "/health/ready");
        health.GetProperty("status").GetString().Should().BeOneOf("Healthy", "Degraded");

        var me = await MorphApiHelper.GetJsonAsync(platform, "/v1/auth/me");
        me.GetProperty("email").GetString().Should().Be(session.Email);

        var plugins = await MorphApiHelper.GetJsonAsync(platform, "/plugins/v1");
        plugins.GetProperty("plugins").ValueKind.Should().Be(JsonValueKind.Array);

        // --- Collections: author + post + comment entities (create → publish → migrate) ---
        var author = await CreatePublishedEntityAsync(
            platform,
            session.EnvironmentId,
            "author",
            "Author",
            [
                Field("name", "Name", required: true, indexed: true),
                Field("bio", "Bio")
            ]);

        var post = await CreatePublishedEntityAsync(
            platform,
            session.EnvironmentId,
            "post",
            "Post",
            [
                Field("title", "Title", required: true, indexed: true),
                Field("slug", "Slug", required: true, indexed: true),
                Field("body", "Body", required: true),
                Field("author_name", "Author Name", indexed: true)
            ]);

        var comment = await CreatePublishedEntityAsync(
            platform,
            session.EnvironmentId,
            "comment",
            "Comment",
            [
                Field("post_slug", "Post Slug", required: true, indexed: true),
                Field("body", "Body", required: true),
                Field("author_name", "Author Name")
            ]);

        var definitions = await MorphApiHelper.GetJsonAsync(
            platform,
            $"/v1/environments/{session.EnvironmentId}/definitions");
        definitions.EnumerateArray().Select(d => d.GetProperty("slug").GetString())
            .Should().Contain(["author", "post", "comment"]);

        var effective = await MorphApiHelper.GetJsonAsync(
            platform,
            $"/v1/environments/{session.EnvironmentId}/definitions/versions/{post.VersionId}/effective-model");
        effective.GetProperty("slug").GetString().Should().Be("post");
        effective.GetProperty("fields").GetArrayLength().Should().BeGreaterThanOrEqualTo(4);

        // --- Records: seed blog content, list, patch, delete ---
        var ada = await MorphApiHelper.PostJsonAsync(data, "/data/v1/author", new
        {
            name = "Ada Lovelace",
            bio = "First programmer"
        });
        ada.GetProperty("fields").GetProperty("name").GetString().Should().Be("Ada Lovelace");

        var hello = await MorphApiHelper.PostJsonAsync(data, "/data/v1/post", new
        {
            title = "Hello Morph",
            slug = "hello-morph",
            body = "Building a community blog on Morph.",
            author_name = "Ada Lovelace"
        });
        var helloId = hello.GetProperty("id").GetGuid();

        await MorphApiHelper.PostJsonAsync(data, "/data/v1/post", new
        {
            title = "Access & Policies",
            slug = "access-policies",
            body = "RBAC for editors and viewers.",
            author_name = "Ada Lovelace"
        });

        var firstComment = await MorphApiHelper.PostJsonAsync(data, "/data/v1/comment", new
        {
            post_slug = "hello-morph",
            body = "Great intro!",
            author_name = "Reader One"
        });
        var commentId = firstComment.GetProperty("id").GetGuid();

        var posts = await MorphApiHelper.GetJsonAsync(data, "/data/v1/post?pageSize=50");
        posts.GetProperty("items").GetArrayLength().Should().Be(2);

        var patched = await MorphApiHelper.SendAsync(
            data,
            HttpMethod.Patch,
            $"/data/v1/post/{helloId}",
            new
            {
                title = "Hello Morph (updated)",
                slug = "hello-morph",
                body = "Building a community blog on Morph — updated.",
                author_name = "Ada Lovelace"
            });
        patched.Response.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var patchDoc = JsonDocument.Parse(patched.Body))
        {
            patchDoc.RootElement.GetProperty("fields").GetProperty("title").GetString()
                .Should().Be("Hello Morph (updated)");
        }

        (await data.DeleteAsync($"/data/v1/comment/{commentId}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        // --- Access: list built-ins, create moderator role, list policies ---
        var roles = await MorphApiHelper.GetJsonAsync(platform, $"/v1/projects/{session.ProjectId}/roles");
        roles.EnumerateArray().Select(r => r.GetProperty("name").GetString())
            .Should().Contain(["owner", "editor", "viewer"]);

        var moderator = await MorphApiHelper.PostJsonAsync(
            platform,
            $"/v1/projects/{session.ProjectId}/roles",
            new { name = "moderator", displayName = "Moderator", inherits = new[] { "editor" } });
        moderator.GetProperty("name").GetString().Should().Be("moderator");

        var policies = await MorphApiHelper.GetJsonAsync(
            platform,
            $"/v1/projects/{session.ProjectId}/policies");
        policies.ValueKind.Should().Be(JsonValueKind.Array);

        // --- Storage: media bucket (panel Storage page) ---
        var bucket = await MorphApiHelper.PostJsonAsync(data, "/files/v1/buckets", new
        {
            slug = "media",
            displayName = "Blog Media"
        });
        bucket.GetProperty("slug").GetString().Should().Be("media");

        // --- Processes: push YAML (CLI-equivalent), then list + run as the panel does ---
        const string processYaml = """
            apiVersion: morph/v1
            kind: Process
            slug: welcome-author
            displayName: Welcome Author
            steps:
              - id: set-greeting
                type: variables.set
                with:
                  name: greeting
                  value: welcome
            """;

        var processUpsert = await MorphApiHelper.PostJsonAsync(
            platform,
            $"/v1/environments/{session.EnvironmentId}/processes",
            new { yaml = processYaml });
        var processVersionId = processUpsert.GetProperty("versionId").GetGuid();
        (await platform.PostAsync(
            $"/v1/environments/{session.EnvironmentId}/processes/versions/{processVersionId}/validate",
            null)).EnsureSuccessStatusCode();
        (await platform.PostAsync(
            $"/v1/environments/{session.EnvironmentId}/processes/versions/{processVersionId}/publish",
            null)).EnsureSuccessStatusCode();

        var processes = await MorphApiHelper.GetJsonAsync(
            platform,
            $"/v1/environments/{session.EnvironmentId}/processes");
        processes.EnumerateArray().Any(p => p.GetProperty("slug").GetString() == "welcome-author")
            .Should().BeTrue();

        var run = await MorphApiHelper.PostJsonAsync(
            platform,
            $"/v1/environments/{session.EnvironmentId}/processes/welcome-author/run",
            new { input = new { author = "Ada Lovelace" } });
        run.GetProperty("status").GetString().Should().Be("Completed");
        var instanceId = run.GetProperty("instanceId").GetGuid();
        var logs = await MorphApiHelper.GetJsonAsync(
            platform,
            $"/v1/environments/{session.EnvironmentId}/processes/instances/{instanceId}/logs");
        logs.GetArrayLength().Should().BeGreaterThan(0);

        // --- Functions: register stub + invoke (panel Functions page) ---
        var fn = await MorphApiHelper.PostJsonAsync(
            platform,
            $"/v1/environments/{session.EnvironmentId}/functions",
            new
            {
                slug = "slugify-title",
                displayName = "Slugify Title",
                runtime = "TypeScriptStub",
                trigger = "Http",
                source = "export default async (ctx) => ctx.input"
            });
        fn.GetProperty("slug").GetString().Should().Be("slugify-title");

        var functions = await MorphApiHelper.GetJsonAsync(
            platform,
            $"/v1/environments/{session.EnvironmentId}/functions");
        functions.EnumerateArray().Any(f => f.GetProperty("slug").GetString() == "slugify-title")
            .Should().BeTrue();

        var invoked = await MorphApiHelper.PostJsonAsync(
            platform,
            $"/v1/environments/{session.EnvironmentId}/functions/slugify-title/invoke",
            new { input = new { title = "Hello Morph" } });
        invoked.GetProperty("succeeded").GetBoolean().Should().BeTrue();

        // --- API page: credentials, GraphQL, governed SQL ---
        var credential = await MorphApiHelper.PostJsonAsync(
            platform,
            $"/v1/projects/{session.ProjectId}/credentials",
            new { name = "blog-ci", environmentId = session.EnvironmentId });
        credential.GetProperty("secret").GetString().Should().StartWith("morph_sk_");
        var credentialId = credential.GetProperty("id").GetGuid();

        var credentials = await MorphApiHelper.GetJsonAsync(
            platform,
            $"/v1/projects/{session.ProjectId}/credentials");
        credentials.EnumerateArray().Any(c => c.GetProperty("id").GetGuid() == credentialId)
            .Should().BeTrue();

        using (var gqlReq = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    query = """
                        query ($entity: String!) {
                          records(entity: $entity, pageSize: 10) {
                            items { id entity }
                            hasMore
                          }
                        }
                        """,
                    variables = new { entity = "post" }
                }),
                Encoding.UTF8,
                "application/json")
        })
        {
            var gqlRes = await data.SendAsync(gqlReq);
            var gqlBody = await gqlRes.Content.ReadAsStringAsync();
            gqlRes.StatusCode.Should().Be(HttpStatusCode.OK, gqlBody);
            using var gqlDoc = JsonDocument.Parse(gqlBody);
            gqlDoc.RootElement.GetProperty("data").GetProperty("records").GetProperty("items")
                .GetArrayLength().Should().Be(2);
        }

        var sql = await MorphApiHelper.PostJsonAsync(data, "/sql/v1/query", new
        {
            sql = "SELECT title, slug FROM post ORDER BY title"
        });
        sql.GetProperty("rowCount").GetInt32().Should().Be(2);

        // --- Webhooks ---
        var webhook = await MorphApiHelper.PostJsonAsync(
            platform,
            $"/v1/projects/{session.ProjectId}/webhooks",
            new
            {
                targetUrl = "https://example.com/hooks/morph-blog",
                environmentId = session.EnvironmentId,
                eventTypes = new[] { "record.created", "record.updated" }
            });
        webhook.GetProperty("targetUrl").GetString().Should().Be("https://example.com/hooks/morph-blog");

        var webhooks = await MorphApiHelper.GetJsonAsync(
            platform,
            $"/v1/projects/{session.ProjectId}/webhooks");
        webhooks.GetArrayLength().Should().BeGreaterThan(0);

        // --- Settings: SMS ---
        var sms = await MorphApiHelper.PostJsonAsync(
            platform,
            "/v1/sms/send",
            new { to = "+15555550123", body = "Your Morph blog is live." });
        sms.GetProperty("succeeded").GetBoolean().Should().BeTrue();

        // Sanity: leftover content still queryable after the full panel tour
        var authors = await MorphApiHelper.GetJsonAsync(data, "/data/v1/author");
        authors.GetProperty("items").GetArrayLength().Should().Be(1);

        // --- Ideal contract: anonymous list/read when Access Allow policy exists; writes stay 401 ---
        var publicRead = await MorphApiHelper.PostJsonAsync(
            platform,
            $"/v1/projects/{session.ProjectId}/policies",
            new
            {
                name = "public.read",
                effect = "Allow",
                actions = new[] { "read", "list" },
                resources = new[] { "entity:post" },
                priority = 10,
                environmentId = session.EnvironmentId
            });
        var publicReadId = publicRead.GetProperty("policyId").GetGuid();
        (await platform.PostAsync(
            $"/v1/projects/{session.ProjectId}/policies/{publicReadId}/publish",
            null)).EnsureSuccessStatusCode();

        using var anon = fixture.CreateClient();
        anon.DefaultRequestHeaders.TryAddWithoutValidation(
            "X-Morph-Env-Id",
            session.EnvironmentId.ToString("D"));

        var anonList = await anon.GetAsync("/data/v1/post?pageSize=50");
        var anonListBody = await anonList.Content.ReadAsStringAsync();
        anonList.StatusCode.Should().Be(HttpStatusCode.OK, anonListBody);
        using (var anonDoc = JsonDocument.Parse(anonListBody))
        {
            anonDoc.RootElement.GetProperty("items").GetArrayLength().Should().Be(2);
        }

        var anonGet = await anon.GetAsync($"/data/v1/post/{helloId}");
        anonGet.StatusCode.Should().Be(HttpStatusCode.OK);

        // Entity without a public Allow policy: Access default-deny (403), not auth challenge.
        var anonPrivate = await anon.GetAsync("/data/v1/author");
        anonPrivate.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var anonWrite = await MorphApiHelper.SendAsync(
            anon,
            HttpMethod.Post,
            "/data/v1/post",
            new
            {
                title = "Should not create",
                slug = "anon-blocked",
                body = "writes require auth",
                author_name = "Nobody"
            });
        anonWrite.Response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        _ = author;
        _ = comment;
    }

    private static object Field(
        string slug,
        string displayName,
        bool required = false,
        bool indexed = false) => new
    {
        fieldId = Guid.Empty,
        slug,
        displayName,
        dataType = "String",
        isRequired = required,
        isIndexed = indexed
    };

    private static async Task<(Guid DefinitionId, Guid VersionId)> CreatePublishedEntityAsync(
        HttpClient platform,
        Guid environmentId,
        string slug,
        string displayName,
        object[] fields)
    {
        var created = await MorphApiHelper.PostJsonAsync(
            platform,
            $"/v1/environments/{environmentId}/definitions",
            new { slug, displayName, kind = "Entity", fields });
        var definitionId = created.GetProperty("definitionId").GetGuid();
        var versionId = created.GetProperty("versionId").GetGuid();

        (await platform.PostAsync(
            $"/v1/environments/{environmentId}/definitions/versions/{versionId}/publish",
            null)).EnsureSuccessStatusCode();

        await MorphApiHelper.PostJsonAsync(
            platform,
            $"/v1/environments/{environmentId}/migrations/plan",
            new { toVersionId = versionId });
        var applied = await MorphApiHelper.PostJsonAsync(
            platform,
            $"/v1/environments/{environmentId}/migrations/apply",
            new { toVersionId = versionId });
        applied.GetProperty("status").GetString().Should().Be("Completed");

        return (definitionId, versionId);
    }
}
