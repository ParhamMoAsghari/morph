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
using Morph.DataPlane.Sql;
using Morph.EntityEngine.Composition;
using Morph.EntityEngine.Domain;
using Morph.EntityEngine.Publishing;
using Morph.EntityEngine.Services;
using Morph.EntityEngine.Storage;
using Morph.EntityEngine.Validation;
using Morph.Infrastructure.Persistence;
using Morph.Infrastructure.Persistence.EntityEngine;
using Morph.Infrastructure.Storage;
using Morph.Runtime.Api.GraphQL;
using Npgsql;
using Xunit;

namespace Morph.DataPlane.Integration.Tests;

[Collection("dataplane-postgres")]
public sealed class GraphQlAndGovernedSqlAccessTests
{
    private readonly PostgresFixture _postgres;

    public GraphQlAndGovernedSqlAccessTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    public async Task Graphql_field_and_sql_path_deny_access_and_allow_happy_select()
    {
        await using var db = _postgres.CreateDbContext();
        await using var dataSource = NpgsqlDataSource.Create(_postgres.ConnectionString);

        var projectId = Guid.NewGuid();
        var envId = Guid.NewGuid();
        var schema = "p_gql_" + Guid.NewGuid().ToString("N")[..8];
        var editorId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();

        var harness = await CreateHarnessAsync(db, dataSource, projectId, envId, schema);

        var title = Field("title", required: true);
        title.IsIndexed = true;
        var create = await harness.Definitions.CreateAsync(
            envId, "note", "Note", DefinitionKind.Entity, [title]);
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
            PrincipalId = editorId.ToString("D"),
            RoleName = BuiltInAppRoles.Editor,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await harness.Roles.SaveBindingAsync(new RoleBinding
        {
            BindingId = Guid.NewGuid(),
            ProjectId = projectId,
            PrincipalId = strangerId.ToString("D"),
            RoleName = BuiltInAppRoles.Authenticated,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var editor = new AccessSubject { PrincipalId = editorId.ToString("D") };
        var stranger = new AccessSubject { PrincipalId = strangerId.ToString("D") };

        var created = await harness.Graphql.CreateRecordAsync(
            envId,
            "note",
            new Dictionary<string, object?> { ["title"] = "hello-sql" },
            editor,
            CancellationToken.None);
        created.Id.Should().NotBeEmpty();
        created.Fields["title"]!.ToString().Should().Be("hello-sql");

        // GraphQL / DataPlane Access deny for principal without allow.
        var deniedAct = async () => await harness.Graphql.GetRecordAsync(
            envId, "note", created.Id, stranger, CancellationToken.None);
        var denied = await deniedAct.Should().ThrowAsync<HotChocolate.GraphQLException>();
        denied.Which.Errors.Should().Contain(e => e.Code == "FORBIDDEN");

        // Governed SQL Access deny (no sql.execute / list for authenticated-only).
        var sqlDenied = await harness.Sql.ExecuteAsync(
            envId,
            "SELECT id FROM note",
            parameters: null,
            stranger,
            CancellationToken.None);
        sqlDenied.IsFailure.Should().BeTrue();
        sqlDenied.Error!.Code.Should().Be("forbidden");

        // Happy path SELECT via governed SQL (Testcontainers Postgres).
        // Field slug `title` is rewritten to the hybrid typed column.
        var sqlOk = await harness.Sql.ExecuteAsync(
            envId,
            "SELECT id, title FROM note WHERE title = @title",
            new Dictionary<string, object?> { ["title"] = "hello-sql" },
            editor,
            CancellationToken.None);
        sqlOk.IsSuccess.Should().BeTrue(sqlOk.Error?.Message);
        sqlOk.Value.Kind.Should().Be(GovernedSqlKind.Select);
        sqlOk.Value.RowCount.Should().Be(1);
        sqlOk.Value.Rows[0].Keys.Should().Contain(k => k.Equals("id", StringComparison.OrdinalIgnoreCase));


        // DDL denied by parser before execution.
        var ddl = await harness.Sql.ExecuteAsync(
            envId, "DROP TABLE note", null, editor, CancellationToken.None);
        ddl.IsFailure.Should().BeTrue();
        ddl.Error!.Code.Should().Be("validation");
    }

    private static FieldDefinition Field(string slug, bool required = false) => new()
    {
        FieldId = Guid.NewGuid(),
        Slug = slug,
        DisplayName = slug,
        DataType = FieldDataType.String,
        IsRequired = required
    };

    private static async Task<Harness> CreateHarnessAsync(
        PlatformDbContext db,
        NpgsqlDataSource dataSource,
        Guid projectId,
        Guid envId,
        string schema)
    {
        await Task.CompletedTask;
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
        var graphql = new MorphDataGraphqlService(data);
        var sql = new GovernedSqlService(cache, access, dataSource, new GovernedSqlOptions { MaxRows = 100 });

        return new Harness(defService, migService, roles, policies, data, cache, graphql, sql);
    }

    private sealed record Harness(
        EntityDefinitionService Definitions,
        MigrationService Migrations,
        IRoleStore Roles,
        IPolicyStore Policies,
        DataPlaneService Data,
        IPublishedModelCache Cache,
        MorphDataGraphqlService Graphql,
        GovernedSqlService Sql);

    private sealed class StaticEnvResolver(Guid envId, Guid projectId, string schema) : IEnvironmentSchemaResolver
    {
        public Task<EnvironmentSchemaInfo?> ResolveAsync(
            Guid environmentId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EnvironmentSchemaInfo?>(
                environmentId == envId ? new EnvironmentSchemaInfo(envId, projectId, schema) : null);
    }
}
