using FluentAssertions;
using Morph.Infrastructure.Persistence.ProcessEngine;
using Morph.ProcessEngine.Domain;
using Xunit;

namespace Morph.DataPlane.Integration.Tests;

[Collection("dataplane-postgres")]
public sealed class ProcessEngineEfPersistenceTests
{
    private readonly PostgresFixture _fx;

    public ProcessEngineEfPersistenceTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Definition_and_instance_round_trip_via_ef()
    {
        await using var db = _fx.CreateDbContext();
        var definitions = new EfProcessDefinitionStore(db);
        var instances = new EfProcessInstanceStore(db);

        var envId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await definitions.SaveDefinitionAsync(new ProcessDefinition
        {
            DefinitionId = definitionId,
            EnvironmentId = envId,
            ProjectId = projectId,
            Slug = "onboard",
            DisplayName = "Onboard",
            CreatedAt = now,
            UpdatedAt = now
        });

        await definitions.SaveVersionAsync(new ProcessDefinitionVersion
        {
            VersionId = versionId,
            DefinitionId = definitionId,
            EnvironmentId = envId,
            Slug = "onboard",
            DisplayName = "Onboard",
            VersionNumber = 1,
            Status = ProcessVersionStatus.Published,
            AuthoringJson = """{"slug":"onboard"}""",
            CompiledJson = """{"steps":[]}""",
            CreatedAt = now,
            PublishedAt = now
        });

        var loaded = await definitions.GetDefinitionBySlugAsync(envId, "onboard");
        loaded.Should().NotBeNull();
        loaded!.DisplayName.Should().Be("Onboard");

        var instanceId = Guid.NewGuid();
        await instances.SaveAsync(new ProcessInstance
        {
            InstanceId = instanceId,
            DefinitionId = definitionId,
            VersionId = versionId,
            EnvironmentId = envId,
            ProjectId = projectId,
            Slug = "onboard",
            Status = ProcessInstanceStatus.Completed,
            VariablesJson = "{}",
            CreatedAt = now,
            UpdatedAt = now,
            CompletedAt = now
        });

        await instances.AppendLogAsync(new ProcessActivityLogEntry
        {
            LogId = Guid.NewGuid(),
            InstanceId = instanceId,
            StepId = "set",
            ActivityType = "variables.set",
            Status = "Completed",
            StartedAt = now,
            CompletedAt = now
        });

        var roundTrip = await instances.GetAsync(instanceId);
        roundTrip.Should().NotBeNull();
        roundTrip!.Status.Should().Be(ProcessInstanceStatus.Completed);

        var logs = await instances.ListLogsAsync(instanceId);
        logs.Should().ContainSingle(l => l.ActivityType == "variables.set");
    }
}
