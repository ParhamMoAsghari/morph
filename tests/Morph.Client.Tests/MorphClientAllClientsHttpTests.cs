using System.Net;
using FluentAssertions;
using Morph.Client;
using Morph.Client.Models;
using Xunit;
using static Morph.Client.Tests.MorphClientTestHelpers;

namespace Morph.Client.Tests;

public sealed class MorphClientAllClientsHttpTests
{
    private static readonly Guid EnvId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ProjId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid DefId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid VerId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid RecId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid BucketId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid FileId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid InstId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid PolicyId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task AuthClient_hits_register_me_and_set_token()
    {
        var handler = new StubHandler((req, _) =>
        {
            return req.RequestUri!.AbsolutePath switch
            {
                "/v1/auth/register" => JsonOk(
                    $$"""{"id":"{{ProjId:D}}","email":"a@b.c","displayName":"Ada"}"""),
                "/v1/auth/me" => JsonOk(
                    $$"""{"id":"{{ProjId:D}}","email":"a@b.c","emailConfirmed":true,"displayName":"Ada"}"""),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };
        });

        using var client = CreateClient(handler, o => o.AccessToken = "tok");
        client.Auth.SetAccessToken("tok2");
        client.Options.AccessToken.Should().Be("tok2");

        var reg = await client.Auth.RegisterAsync(new RegisterRequest("a@b.c", "p", "Ada"));
        reg.Email.Should().Be("a@b.c");

        var me = await client.Auth.GetMeAsync();
        me.DisplayName.Should().Be("Ada");
    }

    [Fact]
    public async Task TenancyClient_hits_all_public_methods()
    {
        var orgId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var handler = new StubHandler((req, _) => req.RequestUri!.AbsolutePath switch
        {
            "/v1/orgs" when req.Method == HttpMethod.Post => JsonOk(
                $$"""{"id":"{{orgId:D}}","name":"Acme","slug":"acme"}"""),
            "/v1/orgs" => JsonOk($$"""[{"id":"{{orgId:D}}","name":"Acme","slug":"acme"}]"""),
            "/v1/projects" when req.Method == HttpMethod.Post => JsonOk(
                $$"""{"id":"{{ProjId:D}}","organizationId":"{{orgId:D}}","name":"App","slug":"app"}"""),
            "/v1/projects" => JsonOk($$"""[{"id":"{{ProjId:D}}","organizationId":"{{orgId:D}}","name":"App","slug":"app"}]"""),
            var p when p == $"/v1/projects/{ProjId:D}/environments" && req.Method == HttpMethod.Post => JsonOk(
                $$"""{"id":"{{EnvId:D}}","projectId":"{{ProjId:D}}","name":"Dev","slug":"dev"}"""),
            var p when p == $"/v1/projects/{ProjId:D}/environments" => JsonOk(
                $$"""[{"id":"{{EnvId:D}}","projectId":"{{ProjId:D}}","name":"Dev","slug":"dev"}]"""),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });

        using var client = CreateClient(handler);
        (await client.Tenancy.CreateOrganizationAsync(new CreateOrganizationRequest("Acme", "acme"))).Slug
            .Should().Be("acme");
        (await client.Tenancy.ListOrganizationsAsync()).Should().ContainSingle(o => o.Slug == "acme");
        (await client.Tenancy.CreateProjectAsync(new CreateProjectRequest(orgId, "App", "app"))).Slug
            .Should().Be("app");
        (await client.Tenancy.ListProjectsAsync()).Should().ContainSingle();
        (await client.Tenancy.CreateEnvironmentAsync(ProjId, new CreateEnvironmentRequest("Dev", "dev"))).Slug
            .Should().Be("dev");
        (await client.Tenancy.ListEnvironmentsAsync(ProjId)).Should().ContainSingle();
    }

    [Fact]
    public async Task DefinitionsClient_hits_all_public_methods()
    {
        var handler = new StubHandler((req, _) =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path == $"/v1/environments/{EnvId:D}/definitions" && req.Method == HttpMethod.Get)
            {
                return JsonOk($$"""[{"definitionId":"{{DefId:D}}","environmentId":"{{EnvId:D}}","slug":"task","displayName":"Task","kind":"Entity"}]""");
            }

            if (path == $"/v1/environments/{EnvId:D}/definitions" && req.Method == HttpMethod.Post)
            {
                return JsonOk($$"""{"definitionId":"{{DefId:D}}","versionId":"{{VerId:D}}","slug":"task","kind":"Entity","status":"Draft"}""");
            }

            if (path == $"/v1/environments/{EnvId:D}/definitions/compose")
            {
                return JsonOk($$"""{"definitionId":"{{DefId:D}}","versionId":"{{VerId:D}}","slug":"composed","kind":"Entity","status":"Draft"}""");
            }

            if (path == $"/v1/environments/{EnvId:D}/definitions/versions/{VerId:D}")
            {
                return JsonOk($$"""{"versionId":"{{VerId:D}}","definitionId":"{{DefId:D}}","environmentId":"{{EnvId:D}}","slug":"task","displayName":"Task","kind":"Entity","versionNumber":1,"status":"Draft","fields":[]}""");
            }

            if (path.Contains("/publish"))
            {
                return JsonOk($$"""{"versionId":"{{VerId:D}}","definitionId":"{{DefId:D}}","environmentId":"{{EnvId:D}}","slug":"task","displayName":"Task","kind":"Entity","versionNumber":1,"status":"Published","fields":[]}""");
            }

            if (path.Contains("/effective-model"))
            {
                return JsonOk($$"""{"definitionId":"{{DefId:D}}","versionId":"{{VerId:D}}","environmentId":"{{EnvId:D}}","slug":"task","kind":"Entity","versionNumber":1,"fingerprint":"fp","fields":[]}""");
            }

            if (path.Contains("/validate"))
            {
                return JsonOk("""{"valid":true}""");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var client = CreateClient(handler);
        (await client.Definitions.ListAsync(EnvId)).Should().ContainSingle(d => d.Slug == "task");
        (await client.Definitions.CreateAsync(EnvId, new CreateDefinitionRequest("task", "Task", "Entity", null))).Slug
            .Should().Be("task");
        (await client.Definitions.ComposeAsync(EnvId, new ComposeDefinitionRequest("c", "C", "Entity", null, null, null))).Slug
            .Should().Be("composed");
        (await client.Definitions.GetVersionAsync(EnvId, VerId)).Status.Should().Be("Draft");
        (await client.Definitions.PublishAsync(EnvId, VerId)).Status.Should().Be("Published");
        (await client.Definitions.GetEffectiveModelAsync(EnvId, VerId)).Fingerprint.Should().Be("fp");
        (await client.Definitions.ValidateAsync(EnvId, VerId)).Should().NotBeNull();
    }

    [Fact]
    public async Task DataClient_hits_all_public_methods()
    {
        var handler = new StubHandler((req, _) =>
        {
            var path = req.RequestUri!.AbsolutePath;
            var query = req.RequestUri.Query;
            if (path == "/data/v1/employee" && req.Method == HttpMethod.Get)
            {
                query.Should().Contain("filter=");
                query.Should().Contain("sort=");
                query.Should().Contain("pageSize=10");
                query.Should().Contain("cursor=c1");
                return JsonOk("""{"items":[],"hasMore":false}""");
            }

            if (path == $"/data/v1/employee/{RecId:D}" && req.Method == HttpMethod.Get)
            {
                return JsonOk(
                    "{\"id\":\"" + RecId.ToString("D") + "\",\"entity\":\"employee\",\"definitionVersionId\":\"" + VerId.ToString("D") + "\",\"fields\":{}}");
            }

            if (path == "/data/v1/employee" && req.Method == HttpMethod.Post)
            {
                return JsonOk(
                    "{\"id\":\"" + RecId.ToString("D") + "\",\"entity\":\"employee\",\"definitionVersionId\":\"" + VerId.ToString("D") + "\",\"fields\":{\"name\":\"Ada\"}}");
            }

            if (path == $"/data/v1/employee/{RecId:D}" && req.Method == HttpMethod.Patch)
            {
                return JsonOk(
                    "{\"id\":\"" + RecId.ToString("D") + "\",\"entity\":\"employee\",\"definitionVersionId\":\"" + VerId.ToString("D") + "\",\"fields\":{\"name\":\"Bob\"}}");
            }

            if (path == $"/data/v1/employee/{RecId:D}" && req.Method == HttpMethod.Delete)
            {
                return EmptyOk();
            }

            if (path == "/data/v1/_meta/status")
            {
                return JsonOk("""{"ok":true}""");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var client = CreateClient(handler, o =>
        {
            o.AccessToken = "tok";
            o.EnvironmentId = EnvId;
        });

        (await client.Data.ListAsync("employee", "x=1", "name", 10, "c1")).HasMore.Should().BeFalse();
        (await client.Data.GetAsync("employee", RecId)).Entity.Should().Be("employee");
        (await client.Data.CreateAsync("employee", new { name = "Ada" })).Id.Should().Be(RecId);
        (await client.Data.UpdateAsync("employee", RecId, new { name = "Bob" })).Fields.Should().NotBeNull();
        await client.Data.DeleteAsync("employee", RecId);
        (await client.Data.StatusAsync()).GetProperty("ok").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task FilesClient_hits_all_public_methods()
    {
        var handler = new StubHandler((req, _) =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path == "/files/v1/buckets" && req.Method == HttpMethod.Post)
            {
                return JsonOk($$"""{"id":"{{BucketId:D}}","projectId":"{{ProjId:D}}","environmentId":"{{EnvId:D}}","slug":"docs","displayName":"Docs"}""");
            }

            if (path == $"/files/v1/buckets/{BucketId:D}/uploads" && !path.Contains("complete"))
            {
                return JsonOk($$"""{"fileId":"{{FileId:D}}","uploadUrl":"https://up"}""");
            }

            if (path.Contains("/uploads/complete"))
            {
                return EmptyOk();
            }

            if (path == $"/files/v1/{FileId:D}/download")
            {
                return JsonOk($$"""{"fileId":"{{FileId:D}}","downloadUrl":"https://dl"}""");
            }

            if (path == $"/files/v1/{FileId:D}" && req.Method == HttpMethod.Delete)
            {
                return EmptyOk();
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var client = CreateClient(handler, o => o.EnvironmentId = EnvId);
        (await client.Files.CreateBucketAsync("docs", "Docs", 1024, ["image/png"])).Slug.Should().Be("docs");
        (await client.Files.CreateUploadAsync(BucketId, "a.png", "image/png", 100)).UploadUrl.Should().Contain("https");
        await client.Files.CompleteUploadAsync(BucketId, FileId);
        (await client.Files.GetDownloadUrlAsync(FileId)).DownloadUrl.Should().Contain("https");
        await client.Files.DeleteAsync(FileId);
    }

    [Fact]
    public async Task AccessClient_hits_all_public_methods()
    {
        var roleId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var handler = new StubHandler((req, _) =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path == $"/v1/projects/{ProjId:D}/roles" && req.Method == HttpMethod.Get)
            {
                return JsonOk($$"""[{"roleId":"{{roleId:D}}","projectId":"{{ProjId:D}}","name":"viewer"}]""");
            }

            if (path == $"/v1/projects/{ProjId:D}/roles" && req.Method == HttpMethod.Post)
            {
                return JsonOk($$"""{"roleId":"{{roleId:D}}","projectId":"{{ProjId:D}}","name":"editor","displayName":"Editor"}""");
            }

            if (path == $"/v1/projects/{ProjId:D}/role-bindings")
            {
                return JsonOk("""{"bound":true}""");
            }

            if (path == $"/v1/projects/{ProjId:D}/policies" && req.Method == HttpMethod.Get)
            {
                return JsonOk($$"""[{"policyId":"{{PolicyId:D}}","projectId":"{{ProjId:D}}","name":"p1","effect":"Allow","actions":["read"],"resources":["*"],"status":"Draft"}]""");
            }

            if (path == $"/v1/projects/{ProjId:D}/policies" && req.Method == HttpMethod.Post)
            {
                return JsonOk($$"""{"policyId":"{{PolicyId:D}}","projectId":"{{ProjId:D}}","name":"p1","effect":"Allow","actions":["read"],"resources":["*"],"status":"Draft"}""");
            }

            if (path.Contains("/publish"))
            {
                return JsonOk($$"""{"policyId":"{{PolicyId:D}}","projectId":"{{ProjId:D}}","name":"p1","effect":"Allow","actions":["read"],"resources":["*"],"status":"Published"}""");
            }

            if (path == "/v1/access/simulate")
            {
                return JsonOk("""{"decision":"Allow"}""");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var client = CreateClient(handler);
        (await client.Access.ListRolesAsync(ProjId)).Should().ContainSingle(r => r.Name == "viewer");
        (await client.Access.CreateRoleAsync(ProjId, new CreateRoleRequest("editor", "Editor", null))).Name
            .Should().Be("editor");
        (await client.Access.BindRoleAsync(ProjId, new BindRoleRequest("u1", "viewer"))).Should().NotBeNull();
        (await client.Access.ListPoliciesAsync(ProjId)).Should().ContainSingle();
        (await client.Access.CreatePolicyAsync(ProjId, new CreatePolicyRequest("p1", "Allow", ["read"], ["*"]))).Name
            .Should().Be("p1");
        (await client.Access.PublishPolicyAsync(ProjId, PolicyId)).Status.Should().Be("Published");
        (await client.Access.SimulateAsync(new { action = "read" })).Should().NotBeNull();
    }

    [Fact]
    public async Task Migrations_and_Processes_clients_hit_all_methods()
    {
        var handler = new StubHandler((req, _) =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.Contains("/migrations/plan"))
            {
                return JsonOk($$"""{"planId":"{{DefId:D}}","definitionId":"{{DefId:D}}","toVersionId":"{{VerId:D}}","stages":[]}""");
            }

            if (path.Contains("/migrations/apply"))
            {
                return JsonOk($$"""{"runId":"{{RecId:D}}","planId":"{{DefId:D}}","definitionId":"{{DefId:D}}","status":"Completed"}""");
            }

            if (path == $"/v1/environments/{EnvId:D}/processes")
            {
                return JsonOk($$"""[{"definitionId":"{{DefId:D}}","environmentId":"{{EnvId:D}}","projectId":"{{ProjId:D}}","slug":"flow","displayName":"Flow","createdAt":"2026-01-01T00:00:00Z","updatedAt":"2026-01-01T00:00:00Z"}]""");
            }

            if (path.Contains("/processes/push"))
            {
                return JsonOk($$"""{"definitionId":"{{DefId:D}}","versionId":"{{VerId:D}}","slug":"flow","status":"Draft","versionNumber":1}""");
            }

            if (path.Contains("/processes/versions") && path.Contains("/publish"))
            {
                return JsonOk($$"""{"versionId":"{{VerId:D}}","definitionId":"{{DefId:D}}","slug":"flow","status":"Published","versionNumber":1,"publishedAt":"2026-01-01T00:00:00Z"}""");
            }

            if (path.Contains("/authoring"))
            {
                return JsonOk($$"""{"slug":"flow","displayName":"Flow","definitionId":"{{DefId:D}}","versionId":"{{VerId:D}}"}""");
            }

            if (path.Contains("/run"))
            {
                return JsonOk($$"""{"instanceId":"{{InstId:D}}","definitionId":"{{DefId:D}}","versionId":"{{VerId:D}}","environmentId":"{{EnvId:D}}","projectId":"{{ProjId:D}}","slug":"flow","status":"Running","createdAt":"2026-01-01T00:00:00Z","updatedAt":"2026-01-01T00:00:00Z"}""");
            }

            if (path.Contains("/logs"))
            {
                return JsonOk($$"""[{"logId":"{{RecId:D}}","instanceId":"{{InstId:D}}","stepId":"s1","activityType":"Task","status":"Completed","startedAt":"2026-01-01T00:00:00Z"}]""");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var client = CreateClient(handler);
        (await client.Migrations.PlanAsync(EnvId, VerId)).PlanId.Should().Be(DefId);
        (await client.Migrations.ApplyAsync(EnvId, VerId)).Status.Should().Be("Completed");
        (await client.Processes.ListAsync(EnvId)).Should().ContainSingle(p => p.Slug == "flow");
        (await client.Processes.PushAsync(EnvId, new ProcessUpsertRequest { Yaml = "x: 1" })).Slug.Should().Be("flow");
        (await client.Processes.PublishAsync(EnvId, VerId)).Status.Should().Be("Published");
        (await client.Processes.GetAuthoringAsync(EnvId, VerId)).Slug.Should().Be("flow");
        (await client.Processes.RunAsync(EnvId, "flow")).InstanceId.Should().Be(InstId);
        (await client.Processes.LogsAsync(EnvId, InstId)).Should().ContainSingle();
    }

    [Fact]
    public async Task ApiKey_is_sent_as_bearer_when_no_access_token()
    {
        var handler = new StubHandler((req, _) =>
        {
            req.Headers.Authorization!.Scheme.Should().Be("Bearer");
            req.Headers.Authorization.Parameter.Should().Be("morph_sk_x");
            return JsonOk("""{"items":[],"hasMore":false}""");
        });

        using var client = CreateClient(handler, o => o.ApiKey = "morph_sk_x");
        await client.Data.ListAsync("employee");
    }

    [Fact]
    public void MorphClient_without_injected_http_disposes_owned_client()
    {
        using var client = new MorphClient(new MorphClientOptions { BaseAddress = new Uri("http://localhost:9999/") });
        client.Auth.Should().NotBeNull();
        client.Processes.Should().NotBeNull();
    }
}
