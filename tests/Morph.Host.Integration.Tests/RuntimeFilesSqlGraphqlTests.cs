using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Morph.Files.Ports;
using Morph.Files.Storage;
using Morph.Host.Integration.Tests.Fixtures;
using Xunit;

namespace Morph.Host.Integration.Tests;

[Collection("morph-host")]
public sealed class RuntimeFilesSqlGraphqlTests(MorphHostFixture fixture)
{
    [Fact]
    public async Task Files_bucket_upload_complete_download_delete()
    {
        var session = await MorphApiHelper.BootstrapAsync(fixture);
        using var client = session.CreateClient(withEnv: true);

        var meta = await MorphApiHelper.GetJsonAsync(client, "/files/v1/_meta/status");
        meta.GetProperty("status").GetString().Should().Be("ready");

        var bucket = await MorphApiHelper.PostJsonAsync(client, "/files/v1/buckets", new
        {
            slug = $"docs-{Guid.NewGuid():N}"[..12],
            displayName = "Docs"
        });
        var bucketId = bucket.GetProperty("id").GetGuid();

        var upload = await MorphApiHelper.PostJsonAsync(
            client,
            $"/files/v1/buckets/{bucketId}/uploads",
            new
            {
                fileName = "hello.txt",
                contentType = "text/plain",
                sizeBytes = 5
            });

        var fileId = upload.GetProperty("fileId").GetGuid();
        var objectKey = upload.GetProperty("objectKey").GetString();
        objectKey.Should().NotBeNullOrWhiteSpace();
        upload.GetProperty("uploadUrl").GetString().Should().NotBeNullOrWhiteSpace();

        // Client would PUT to the signed URL; with in-memory storage, stage the object directly.
        var storage = fixture.Services.GetRequiredService<IObjectStorage>();
        storage.Should().BeOfType<InMemoryObjectStorage>();
        ((InMemoryObjectStorage)storage).PutObject(
            "morph",
            objectKey!,
            Encoding.UTF8.GetBytes("hello"),
            "text/plain");

        var complete = await MorphApiHelper.PostJsonAsync(
            client,
            $"/files/v1/buckets/{bucketId}/uploads/complete",
            new { fileId });
        complete.GetProperty("id").GetGuid().Should().Be(fileId);
        complete.GetProperty("status").GetString().Should().Be("Ready");

        var download = await MorphApiHelper.GetJsonAsync(client, $"/files/v1/{fileId}/download");
        download.GetProperty("fileId").GetGuid().Should().Be(fileId);
        download.GetProperty("downloadUrl").GetString().Should().NotBeNullOrWhiteSpace();

        var deleted = await client.DeleteAsync($"/files/v1/{fileId}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Files_upload_rejects_disallowed_content_type()
    {
        var session = await MorphApiHelper.BootstrapAsync(fixture);
        using var client = session.CreateClient(withEnv: true);

        var bucket = await MorphApiHelper.PostJsonAsync(client, "/files/v1/buckets", new
        {
            slug = $"img-{Guid.NewGuid():N}"[..12],
            displayName = "Images",
            allowedContentTypes = new[] { "image/png" }
        });
        var bucketId = bucket.GetProperty("id").GetGuid();

        var (response, body) = await MorphApiHelper.SendAsync(
            client,
            HttpMethod.Post,
            $"/files/v1/buckets/{bucketId}/uploads",
            new
            {
                fileName = "note.txt",
                contentType = "text/plain",
                sizeBytes = 12
            });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("Content type", because: "disallowed MIME type should be reported");
    }

    [Fact]
    public async Task Governed_sql_query_and_validation()
    {
        var session = await MorphApiHelper.BootstrapAsync(fixture);
        await MorphApiHelper.CreatePublishMigrateEntityAsync(session, "item", "Item");

        using var data = session.CreateClient(withEnv: true);
        await MorphApiHelper.PostJsonAsync(data, "/data/v1/item", new { title = "A", body = "B" });

        var ok = await MorphApiHelper.PostJsonAsync(data, "/sql/v1/query", new
        {
            sql = "SELECT title FROM item"
        });
        ok.ValueKind.Should().Be(JsonValueKind.Object);

        var (bad, _) = await MorphApiHelper.SendAsync(
            data,
            HttpMethod.Post,
            "/sql/v1/query",
            new { sql = "DROP TABLE item" });
        // DDL is reject-listed by the governed SQL parser before execution.
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var (missingSql, _) = await MorphApiHelper.SendAsync(
            data,
            HttpMethod.Post,
            "/sql/v1/query",
            new { nope = true });
        missingSql.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Governed_sql_query_with_parameters()
    {
        var session = await MorphApiHelper.BootstrapAsync(fixture);
        await MorphApiHelper.CreatePublishMigrateEntityAsync(session, "paramitem", "Param Item");

        using var data = session.CreateClient(withEnv: true);
        await MorphApiHelper.PostJsonAsync(data, "/data/v1/paramitem", new { title = "needle", body = "hay" });
        await MorphApiHelper.PostJsonAsync(data, "/data/v1/paramitem", new { title = "other", body = "stack" });

        var result = await MorphApiHelper.PostJsonAsync(data, "/sql/v1/query", new
        {
            sql = "SELECT title FROM paramitem WHERE title = @title",
            parameters = new { title = "needle" }
        });
        result.GetProperty("rowCount").GetInt32().Should().Be(1);
        result.GetProperty("rows").GetArrayLength().Should().Be(1);
        result.GetProperty("rows")[0].GetRawText().Should().Contain("needle");
    }

    [Fact]
    public async Task GraphQL_endpoint_accepts_authorized_query()
    {
        var session = await MorphApiHelper.BootstrapAsync(fixture);
        await MorphApiHelper.CreatePublishMigrateEntityAsync(session, "gqlnote", "GqlNote");

        using var client = session.CreateClient(withEnv: true);
        await MorphApiHelper.PostJsonAsync(client, "/data/v1/gqlnote", new
        {
            title = "via-gql",
            body = "x"
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    query = "{ __typename }"
                }),
                Encoding.UTF8,
                "application/json")
        };

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        body.Should().Contain("__typename");
    }

    [Fact]
    public async Task GraphQL_schema_introspection_and_records_query()
    {
        var session = await MorphApiHelper.BootstrapAsync(fixture);
        await MorphApiHelper.CreatePublishMigrateEntityAsync(session, "gqlitem", "Gql Item");

        using var client = session.CreateClient(withEnv: true);
        await MorphApiHelper.PostJsonAsync(client, "/data/v1/gqlitem", new
        {
            title = "listed",
            body = "via graphql"
        });

        using var introspection = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    query = """
                        {
                          __schema {
                            queryType { name }
                            mutationType { name }
                          }
                        }
                        """
                }),
                Encoding.UTF8,
                "application/json")
        };
        var introResponse = await client.SendAsync(introspection);
        var introBody = await introResponse.Content.ReadAsStringAsync();
        introResponse.StatusCode.Should().Be(HttpStatusCode.OK, introBody);
        introBody.Should().Contain("MorphGraphqlQueries");

        using var listRequest = new HttpRequestMessage(HttpMethod.Post, "/graphql")
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
                    variables = new { entity = "gqlitem" }
                }),
                Encoding.UTF8,
                "application/json")
        };
        var listResponse = await client.SendAsync(listRequest);
        var listBody = await listResponse.Content.ReadAsStringAsync();
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK, listBody);
        using var listDoc = JsonDocument.Parse(listBody);
        var items = listDoc.RootElement.GetProperty("data").GetProperty("records").GetProperty("items");
        items.GetArrayLength().Should().BeGreaterThan(0);
        items[0].GetProperty("entity").GetString().Should().Be("gqlitem");
    }
}
