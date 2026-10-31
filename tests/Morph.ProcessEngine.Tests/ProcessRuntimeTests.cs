using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Morph.Access.Domain;
using Morph.BuildingBlocks.Time;
using Morph.ProcessEngine.DependencyInjection;
using Morph.ProcessEngine.Domain;
using Morph.ProcessEngine.Ir;
using Morph.ProcessEngine.Persistence;
using Morph.ProcessEngine.Services;
using Xunit;

namespace Morph.ProcessEngine.Tests;

public sealed class ProcessRuntimeTests
{
    [Fact]
    public async Task Start_runs_published_process_to_completion()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock, SystemClock>();
        services.AddMorphProcessEngine();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();

        var definitions = provider.GetRequiredService<ProcessDefinitionService>();
        var runtime = provider.GetRequiredService<ProcessRuntimeService>();

        var envId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var doc = ProcessIrSerializer.ParseYaml("""
            apiVersion: morph/v1
            kind: Process
            slug: greet
            steps:
              - id: set
                type: variables.set
                with:
                  name: message
                  value: ok
            """);

        var upsert = await definitions.UpsertDraftAsync(envId, projectId, doc);
        upsert.IsSuccess.Should().BeTrue();
        var publish = await definitions.PublishAsync(upsert.Value.Version.VersionId);
        publish.IsSuccess.Should().BeTrue();

        var subject = new AccessSubject { PrincipalId = "test", Roles = ["owner"] };
        var run = await runtime.StartAsync(envId, "greet", null, subject);
        run.IsSuccess.Should().BeTrue();
        run.Value.Status.Should().Be(ProcessInstanceStatus.Completed);

        var logs = await runtime.GetLogsAsync(run.Value.InstanceId);
        logs.Should().NotBeEmpty();
    }

    [Fact]
    public async Task InMemory_definition_store_round_trips()
    {
        var store = new InMemoryProcessDefinitionStore();
        var id = Guid.NewGuid();
        var def = new ProcessDefinition
        {
            DefinitionId = id,
            EnvironmentId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            Slug = "x",
            DisplayName = "X",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await store.SaveDefinitionAsync(def);
        (await store.GetDefinitionAsync(id)).Should().NotBeNull();
    }
}
