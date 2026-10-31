using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Morph.BuildingBlocks.Time;
using Morph.ProcessEngine.DependencyInjection;
using Morph.ProcessEngine.Domain;
using Morph.ProcessEngine.Ir;
using Morph.ProcessEngine.Ports;
using Morph.ProcessEngine.Services;
using Xunit;

namespace Morph.ProcessEngine.Tests;

public sealed class ProcessDefinitionServiceEdgeTests
{
    [Fact]
    public async Task UpsertDraftAsync_rejects_empty_slug()
    {
        await using var provider = BuildProvider();
        var definitions = provider.GetRequiredService<ProcessDefinitionService>();

        var doc = new ProcessDocument { Slug = "   ", Steps = [new ProcessStepIr { Id = "s", Type = "variables.set" }] };
        var result = await definitions.UpsertDraftAsync(Guid.NewGuid(), Guid.NewGuid(), doc);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("slug");
    }

    [Fact]
    public async Task PublishAsync_fails_for_missing_version()
    {
        await using var provider = BuildProvider();
        var definitions = provider.GetRequiredService<ProcessDefinitionService>();

        var publish = await definitions.PublishAsync(Guid.NewGuid());

        publish.IsFailure.Should().BeTrue();
        publish.Error!.Code.Should().Be("not_found");
    }

    [Fact]
    public async Task PublishAsync_fails_when_version_already_published()
    {
        await using var provider = BuildProvider();
        var definitions = provider.GetRequiredService<ProcessDefinitionService>();

        var envId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var doc = ProcessIrSerializer.ParseYaml("""
            apiVersion: morph/v1
            kind: Process
            slug: once
            steps:
              - id: s
                type: variables.set
                with:
                  name: x
                  value: 1
            """);

        var upsert = await definitions.UpsertDraftAsync(envId, projectId, doc);
        var versionId = upsert.Value.Version.VersionId;
        (await definitions.PublishAsync(versionId)).IsSuccess.Should().BeTrue();

        var again = await definitions.PublishAsync(versionId);
        again.IsFailure.Should().BeTrue();
        again.Error!.Message.Should().Contain("draft");
    }

    [Fact]
    public async Task GetLogsAsync_returns_empty_for_unknown_instance()
    {
        await using var provider = BuildProvider();
        var runtime = provider.GetRequiredService<ProcessRuntimeService>();

        var logs = await runtime.GetLogsAsync(Guid.NewGuid());
        logs.Should().BeEmpty();
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
