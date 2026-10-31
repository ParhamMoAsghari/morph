using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Morph.Access.Domain;
using Morph.BuildingBlocks.Time;
using Morph.ProcessEngine.DependencyInjection;
using Morph.ProcessEngine.Domain;
using Morph.ProcessEngine.Execution;
using Morph.ProcessEngine.Ir;
using Morph.ProcessEngine.Services;
using Xunit;

namespace Morph.ProcessEngine.Tests;

public sealed class ProcessWaitAndVariablesTests
{
    [Fact]
    public async Task Long_delay_leaves_instance_in_waiting_status()
    {
        await using var provider = BuildProvider();
        var definitions = provider.GetRequiredService<ProcessDefinitionService>();
        var runtime = provider.GetRequiredService<ProcessRuntimeService>();

        var envId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var doc = ProcessIrSerializer.ParseYaml("""
            apiVersion: morph/v1
            kind: Process
            slug: wait-test
            steps:
              - id: pause
                type: delay
                with:
                  seconds: 30
            """);

        var upsert = await definitions.UpsertDraftAsync(envId, projectId, doc);
        (await definitions.PublishAsync(upsert.Value.Version.VersionId)).IsSuccess.Should().BeTrue();

        var run = await runtime.StartAsync(
            envId,
            "wait-test",
            null,
            new AccessSubject { PrincipalId = "test", Roles = ["owner"] });

        run.IsSuccess.Should().BeTrue();
        run.Value.Status.Should().Be(ProcessInstanceStatus.Waiting);
        run.Value.WaitingUntil.Should().NotBeNull();
    }

    [Fact]
    public async Task Variables_set_then_string_concat_produces_output_variable()
    {
        await using var provider = BuildProvider();
        var definitions = provider.GetRequiredService<ProcessDefinitionService>();
        var runtime = provider.GetRequiredService<ProcessRuntimeService>();

        var envId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var doc = ProcessIrSerializer.ParseYaml("""
            apiVersion: morph/v1
            kind: Process
            slug: concat-test
            steps:
              - id: set
                type: variables.set
                with:
                  name: greeting
                  value: hello
              - id: concat
                type: string.concat
                with:
                  parts: ["{{greeting}}", " morph"]
                  resultVar: message
            """);

        var upsert = await definitions.UpsertDraftAsync(envId, projectId, doc);
        (await definitions.PublishAsync(upsert.Value.Version.VersionId)).IsSuccess.Should().BeTrue();

        var run = await runtime.StartAsync(
            envId,
            "concat-test",
            null,
            new AccessSubject { PrincipalId = "test", Roles = ["owner"] });

        run.IsSuccess.Should().BeTrue();
        run.Value.Status.Should().Be(ProcessInstanceStatus.Completed);

        var variables = VariableBag.FromJson(run.Value.VariablesJson);
        variables.TryGet("message", out var message).Should().BeTrue();
        message.Should().Be("hello morph");
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock, SystemClock>();
        services.AddMorphProcessEngine();
        services.AddLogging();
        return services.BuildServiceProvider();
    }
}
