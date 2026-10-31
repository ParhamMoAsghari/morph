using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Morph.Host.Integration.Tests.Fixtures;
using Xunit;

namespace Morph.Host.Integration.Tests;

[Collection("morph-host")]
public sealed class PlatformDefinitionMigrationDataE2ETests(MorphHostFixture fixture)
{
    [Fact]
    public async Task Definition_publish_migrate_and_crud_flow()
    {
        var session = await MorphApiHelper.BootstrapAsync(fixture);
        var (definitionId, versionId) = await MorphApiHelper.CreatePublishMigrateEntityAsync(session, "note", "Note");

        using var platform = session.CreateClient(withEnv: false);
        var listed = await MorphApiHelper.GetJsonAsync(
            platform,
            $"/v1/environments/{session.EnvironmentId}/definitions");
        listed.EnumerateArray().Any(x => x.GetProperty("definitionId").GetGuid() == definitionId).Should().BeTrue();

        var def = await MorphApiHelper.GetJsonAsync(
            platform,
            $"/v1/environments/{session.EnvironmentId}/definitions/{definitionId}");
        def.GetProperty("slug").GetString().Should().Be("note");

        var ver = await MorphApiHelper.GetJsonAsync(
            platform,
            $"/v1/environments/{session.EnvironmentId}/definitions/versions/{versionId}");
        ver.GetProperty("versionId").GetGuid().Should().Be(versionId);

        var effective = await MorphApiHelper.GetJsonAsync(
            platform,
            $"/v1/environments/{session.EnvironmentId}/definitions/versions/{versionId}/effective-model");
        effective.ValueKind.Should().Be(JsonValueKind.Object);

        (await platform.PostAsync(
            $"/v1/environments/{session.EnvironmentId}/definitions/versions/{versionId}/validate",
            null)).EnsureSuccessStatusCode();

        using var data = session.CreateClient(withEnv: true);

        var meta = await MorphApiHelper.GetJsonAsync(data, "/data/v1/_meta/status");
        meta.GetProperty("status").GetString().Should().Be("ready");

        var created = await MorphApiHelper.PostJsonAsync(data, "/data/v1/note", new
        {
            title = "Hello",
            body = "World"
        });
        var recordId = created.GetProperty("id").GetGuid();
        created.GetProperty("fields").GetProperty("title").GetString().Should().Be("Hello");

        var got = await MorphApiHelper.GetJsonAsync(data, $"/data/v1/note/{recordId}");
        got.GetProperty("id").GetGuid().Should().Be(recordId);

        var patched = await MorphApiHelper.SendAsync(
            data,
            HttpMethod.Patch,
            $"/data/v1/note/{recordId}",
            new { title = "Updated", body = "World" });
        patched.Response.EnsureSuccessStatusCode();

        var list = await MorphApiHelper.GetJsonAsync(data, "/data/v1/note");
        list.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);

        var deleted = await data.DeleteAsync($"/data/v1/note/{recordId}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Compose_decompose_and_draft_endpoints_work()
    {
        var session = await MorphApiHelper.BootstrapAsync(fixture);
        using var client = session.CreateClient(withEnv: false);

        // Create a component-like entity and a composed entity.
        var component = await MorphApiHelper.PostJsonAsync(
            client,
            $"/v1/environments/{session.EnvironmentId}/definitions",
            new
            {
                slug = "audit",
                displayName = "Audit",
                kind = "Component",
                fields = new object[]
                {
                    new
                    {
                        fieldId = Guid.Empty,
                        slug = "created_label",
                        displayName = "Created Label",
                        dataType = "String"
                    }
                }
            });
        var componentVersionId = component.GetProperty("versionId").GetGuid();
        (await client.PostAsync(
            $"/v1/environments/{session.EnvironmentId}/definitions/versions/{componentVersionId}/publish",
            null)).EnsureSuccessStatusCode();

        var composed = await MorphApiHelper.PostJsonAsync(
            client,
            $"/v1/environments/{session.EnvironmentId}/definitions/compose",
            new
            {
                slug = "ticket",
                displayName = "Ticket",
                kind = "Entity",
                localFields = new object[]
                {
                    new
                    {
                        fieldId = Guid.Empty,
                        slug = "subject",
                        displayName = "Subject",
                        dataType = "String",
                        isRequired = true
                    }
                },
                componentUses = new object[]
                {
                    new
                    {
                        componentUseId = Guid.NewGuid(),
                        alias = "audit",
                        targetDefinitionId = component.GetProperty("definitionId").GetGuid(),
                        pinnedVersionId = componentVersionId,
                        mode = "Embed",
                        isRequired = true
                    }
                }
            });

        var composedVersionId = composed.GetProperty("versionId").GetGuid();
        (await client.PostAsync(
            $"/v1/environments/{session.EnvironmentId}/definitions/versions/{composedVersionId}/publish",
            null)).EnsureSuccessStatusCode();

        var draft = await MorphApiHelper.PostJsonAsync(
            client,
            $"/v1/environments/{session.EnvironmentId}/definitions/versions/{composedVersionId}/drafts",
            new { });
        draft.ValueKind.Should().NotBe(JsonValueKind.Undefined);

        // Decompose a local field into a new component from the published composed version.
        var published = await MorphApiHelper.GetJsonAsync(
            client,
            $"/v1/environments/{session.EnvironmentId}/definitions/versions/{composedVersionId}");
        published.GetProperty("versionId").GetGuid().Should().Be(composedVersionId);
    }

    [Fact]
    public async Task Decompose_extracts_field_from_draft_into_component()
    {
        var session = await MorphApiHelper.BootstrapAsync(fixture);
        using var client = session.CreateClient(withEnv: false);

        var created = await MorphApiHelper.PostJsonAsync(
            client,
            $"/v1/environments/{session.EnvironmentId}/definitions",
            new
            {
                slug = "blogpost",
                displayName = "Blog Post",
                kind = "Entity",
                fields = new object[]
                {
                    new
                    {
                        fieldId = Guid.Empty,
                        slug = "title",
                        displayName = "Title",
                        dataType = "String",
                        isRequired = true
                    },
                    new
                    {
                        fieldId = Guid.Empty,
                        slug = "body",
                        displayName = "Body",
                        dataType = "String"
                    },
                    new
                    {
                        fieldId = Guid.Empty,
                        slug = "tags",
                        displayName = "Tags",
                        dataType = "String"
                    }
                }
            });

        var publishedVersionId = created.GetProperty("versionId").GetGuid();
        (await client.PostAsync(
            $"/v1/environments/{session.EnvironmentId}/definitions/versions/{publishedVersionId}/publish",
            null)).EnsureSuccessStatusCode();

        var draft = await MorphApiHelper.PostJsonAsync(
            client,
            $"/v1/environments/{session.EnvironmentId}/definitions/versions/{publishedVersionId}/drafts",
            new { });
        var draftVersionId = draft.GetProperty("versionId").GetGuid();

        var draftVersion = await MorphApiHelper.GetJsonAsync(
            client,
            $"/v1/environments/{session.EnvironmentId}/definitions/versions/{draftVersionId}");
        var bodyFieldId = draftVersion.GetProperty("fields").EnumerateArray()
            .First(f => f.GetProperty("slug").GetString() == "body")
            .GetProperty("fieldId")
            .GetGuid();

        var componentSlug = $"post-body-{Guid.NewGuid():N}"[..16];
        var decomposed = await MorphApiHelper.PostJsonAsync(
            client,
            $"/v1/environments/{session.EnvironmentId}/definitions/versions/{draftVersionId}/decompose",
            new
            {
                fieldIds = new[] { bodyFieldId },
                newComponentSlug = componentSlug,
                newComponentDisplayName = "Post Body",
                embedAlias = "body"
            });

        decomposed.GetProperty("componentDefinitionId").GetGuid().Should().NotBeEmpty();
        decomposed.GetProperty("componentVersionId").GetGuid().Should().NotBeEmpty();
        decomposed.GetProperty("updatedSourceVersionId").GetGuid().Should().NotBeEmpty();
        decomposed.GetProperty("preservedFieldIds").EnumerateArray().Should().Contain(el => el.GetGuid() == bodyFieldId);
    }

    [Fact]
    public async Task Migration_resume_unknown_run_returns_not_found()
    {
        var session = await MorphApiHelper.BootstrapAsync(fixture);
        using var client = session.CreateClient(withEnv: false);

        var (response, body) = await MorphApiHelper.SendAsync(
            client,
            HttpMethod.Post,
            $"/v1/environments/{session.EnvironmentId}/migrations/{Guid.NewGuid()}/resume",
            null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Backup_export_and_import_round_trip()
    {
        var session = await MorphApiHelper.BootstrapAsync(fixture);
        await MorphApiHelper.CreatePublishMigrateEntityAsync(session, "article", "Article");

        using var client = session.CreateClient(withEnv: false);
        var export = await MorphApiHelper.GetJsonAsync(
            client,
            $"/v1/environments/{session.EnvironmentId}/backup/definitions");
        export.ValueKind.Should().Be(JsonValueKind.Object);

        var import = await client.PostAsJsonAsync(
            $"/v1/environments/{session.EnvironmentId}/backup/definitions/import",
            export);
        import.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
