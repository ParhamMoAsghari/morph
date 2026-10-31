using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Morph.Access.Domain;
using Morph.Access.Evaluation;
using Morph.Access.Persistence;
using Morph.Access.Ports;
using Morph.BuildingBlocks.Time;
using Morph.DataPlane.Cache;
using Morph.DataPlane.Records;
using Morph.DataPlane.Services;
using Morph.EntityEngine.Composition;
using Morph.EntityEngine.Domain;
using Morph.EntityEngine.Publishing;
using Morph.EntityEngine.Services;
using Morph.EntityEngine.Storage;
using Morph.EntityEngine.Validation;
using Morph.Infrastructure.Persistence;
using Morph.Infrastructure.Persistence.EntityEngine;
using Morph.Infrastructure.Storage;
using Npgsql;
using Xunit;

namespace Morph.DataPlane.Integration.Tests;

[Collection("dataplane-postgres")]
public sealed class AccessDataVerticalSliceTests
{
    private readonly PostgresFixture _postgres;

    public AccessDataVerticalSliceTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    public async Task Role_allow_abac_owner_and_pbac_field_deny_for_viewer()
    {
        await using var db = _postgres.CreateDbContext();
        await using var dataSource = NpgsqlDataSource.Create(_postgres.ConnectionString);

        var projectId = Guid.NewGuid();
        var envId = Guid.NewGuid();
        var schema = "p_dp_" + Guid.NewGuid().ToString("N")[..8];
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();

        var harness = CreateHarness(db, dataSource, projectId, envId, schema);

        var title = Field("title", required: true);
        title.IsIndexed = true;
        var secret = Field("secret");
        var create = await harness.Definitions.CreateAsync(
            envId, "note", "Note", DefinitionKind.Entity, [title, secret]);
        create.IsSuccess.Should().BeTrue(create.Error?.Message);

        var publish = await harness.Definitions.PublishAsync(create.Value.Version.VersionId);
        publish.IsSuccess.Should().BeTrue(publish.Error?.Message);

        var plan = await harness.Migrations.PlanCreateAsync(publish.Value.VersionId, schema);
        plan.IsSuccess.Should().BeTrue(plan.Error?.Message);
        var apply = await harness.Migrations.ApplyAsync(plan.Value.Plan);
        apply.IsSuccess.Should().BeTrue(apply.Error?.Message);
        harness.Cache.InvalidateEnvironment(envId);

        await harness.Roles.EnsureBuiltInRolesAsync(projectId, DateTimeOffset.UtcNow);

        await harness.Roles.SaveBindingAsync(new RoleBinding
        {
            BindingId = Guid.NewGuid(),
            ProjectId = projectId,
            PrincipalId = ownerId.ToString("D"),
            RoleName = BuiltInAppRoles.Editor,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await harness.Roles.SaveBindingAsync(new RoleBinding
        {
            BindingId = Guid.NewGuid(),
            ProjectId = projectId,
            PrincipalId = otherId.ToString("D"),
            RoleName = BuiltInAppRoles.Authenticated,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await harness.Roles.SaveBindingAsync(new RoleBinding
        {
            BindingId = Guid.NewGuid(),
            ProjectId = projectId,
            PrincipalId = viewerId.ToString("D"),
            RoleName = BuiltInAppRoles.Viewer,
            CreatedAt = DateTimeOffset.UtcNow
        });

        // ABAC: authenticated (non-RBAC-read) may read only own notes
        await harness.Policies.SaveAsync(new PolicyDocument
        {
            PolicyId = Guid.NewGuid(),
            ProjectId = projectId,
            Name = "owner-read-note",
            Effect = AccessEffect.Allow,
            Actions = [AccessActions.Read, AccessActions.List],
            Resources = ["entity:note"],
            Condition = ConditionExpression.Eq("resource.owner_id", "subject.sub"),
            EmitRowFilterObligation = true,
            Priority = 20,
            Status = PolicyDocumentStatus.Published,
            CreatedAt = DateTimeOffset.UtcNow,
            PublishedAt = DateTimeOffset.UtcNow
        });

        // PBAC field deny for viewer only (exclude editor/owner who inherit viewer)
        await harness.Policies.SaveAsync(new PolicyDocument
        {
            PolicyId = Guid.NewGuid(),
            ProjectId = projectId,
            Name = "viewer-hide-secret",
            Effect = AccessEffect.Deny,
            Actions = [AccessActions.Read, AccessActions.List],
            Resources = ["entity:note/fields:secret"],
            FieldMaskOnly = true,
            Condition = ConditionExpression.And(
                new ConditionExpression
                {
                    Op = "in",
                    Left = ConditionOperand.Literal("viewer"),
                    Right = ConditionOperand.Attr("subject.roles")
                },
                ConditionExpression.Not(new ConditionExpression
                {
                    Op = "in",
                    Left = ConditionOperand.Literal("editor"),
                    Right = ConditionOperand.Attr("subject.roles")
                })),
            Priority = 50,
            Status = PolicyDocumentStatus.Published,
            CreatedAt = DateTimeOffset.UtcNow,
            PublishedAt = DateTimeOffset.UtcNow
        });

        var ownerSubject = new AccessSubject { PrincipalId = ownerId.ToString("D") };
        var otherSubject = new AccessSubject { PrincipalId = otherId.ToString("D") };
        var viewerSubject = new AccessSubject { PrincipalId = viewerId.ToString("D") };

        // 1) RBAC role allow — editor can create
        var created = await harness.Data.CreateAsync(
            envId,
            "note",
            new Dictionary<string, object?> { ["title"] = "hello", ["secret"] = "top-secret" },
            ownerSubject);
        created.IsSuccess.Should().BeTrue(created.Error?.Message);
        created.Value.Fields["title"]!.ToString().Should().Be("hello");
        created.Value.CreatedBy.Should().Be(ownerId);
        created.Value.Fields.Should().ContainKey("secret");

        // 2) ABAC owner check — other authenticated cannot read owner's note
        var foreignRead = await harness.Data.GetAsync(envId, "note", created.Value.Id, otherSubject);
        foreignRead.IsFailure.Should().BeTrue();
        foreignRead.Error!.Code.Should().Be("forbidden");

        // Owner (editor) can read including secret
        var ownerRead = await harness.Data.GetAsync(envId, "note", created.Value.Id, ownerSubject);
        ownerRead.IsSuccess.Should().BeTrue(ownerRead.Error?.Message);
        ownerRead.Value.Fields.Should().ContainKey("secret");

        // 3) PBAC field deny for viewer — can read but secret masked
        var viewerRead = await harness.Data.GetAsync(envId, "note", created.Value.Id, viewerSubject);
        viewerRead.IsSuccess.Should().BeTrue(viewerRead.Error?.Message);
        viewerRead.Value.Fields.Should().ContainKey("title");
        viewerRead.Value.Fields.Should().NotContainKey("secret");

        var list = await harness.Data.ListAsync(
            envId,
            "note",
            new DynamicListRequest { PageSize = 10 },
            viewerSubject);
        list.IsSuccess.Should().BeTrue(list.Error?.Message);
        list.Value.Items.Should().ContainSingle();
        list.Value.Items[0].Fields.Should().NotContainKey("secret");
    }

    private static FieldDefinition Field(string slug, bool required = false) => new()
    {
        FieldId = Guid.NewGuid(),
        Slug = slug,
        DisplayName = slug,
        DataType = FieldDataType.String,
        IsRequired = required
    };

    private static Harness CreateHarness(
        PlatformDbContext db,
        NpgsqlDataSource dataSource,
        Guid projectId,
        Guid envId,
        string schema)
    {
        var clock = new SystemClock();
        var definitions = new EfDefinitionStore(db);
        var runs = new EfMigrationRunStore(db);
        var mappings = new EfStorageMappingStore(db);
        var ddl = new NpgsqlDdlExecutor(db);

        var validator = new DefinitionGraphValidator(definitions);
        var compiler = new EffectiveModelCompiler(definitions, validator, clock);
        var composition = new CompositionOperations(definitions, clock);
        var decomposition = new DecompositionOperations(definitions, clock);
        var publisher = new DefinitionPublisher(definitions, compiler, clock);
        var defService = new EntityDefinitionService(
            definitions, validator, compiler, composition, decomposition, publisher, clock);

        var mapper = new StorageMapper(clock);
        var planner = new MigrationPlanner(clock);
        var executor = new StagedMigrationExecutor(ddl, runs, definitions, mappings, clock);
        var migService = new MigrationService(definitions, mapper, planner, executor, mappings);

        var roles = new InMemoryRoleStore();
        var policies = new InMemoryPolicyStore();
        var access = new AccessEvaluator(roles, policies, new RoleHierarchyExpander(roles));

        var services = new ServiceCollection();
        services.AddSingleton<IClock>(clock);
        services.AddSingleton(definitions);
        services.AddSingleton<Morph.EntityEngine.Ports.IDefinitionStore>(definitions);
        services.AddSingleton(mappings);
        services.AddSingleton<Morph.EntityEngine.Ports.IStorageMappingStore>(mappings);
        services.AddSingleton(mapper);
        services.AddSingleton<IEnvironmentSchemaResolver>(new StaticEnvResolver(envId, projectId, schema));
        services.AddSingleton(dataSource);
        var sp = services.BuildServiceProvider();

        var cache = new MemoryPublishedModelCache(sp.GetRequiredService<IServiceScopeFactory>());
        var records = new NpgsqlDynamicRecordRepository(dataSource, clock);
        var data = new DataPlaneService(cache, records, access);

        return new Harness(defService, migService, roles, policies, data, cache);
    }

    private sealed record Harness(
        EntityDefinitionService Definitions,
        MigrationService Migrations,
        IRoleStore Roles,
        IPolicyStore Policies,
        DataPlaneService Data,
        IPublishedModelCache Cache);

    private sealed class StaticEnvResolver(Guid envId, Guid projectId, string schema) : IEnvironmentSchemaResolver
    {
        public Task<EnvironmentSchemaInfo?> ResolveAsync(
            Guid environmentId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EnvironmentSchemaInfo?>(
                environmentId == envId ? new EnvironmentSchemaInfo(envId, projectId, schema) : null);
    }
}
