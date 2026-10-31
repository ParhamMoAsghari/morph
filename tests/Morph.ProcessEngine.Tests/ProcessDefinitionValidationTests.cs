using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Morph.BuildingBlocks.Time;
using Morph.ProcessEngine.DependencyInjection;
using Morph.ProcessEngine.Ir;
using Morph.ProcessEngine.Publishing;
using Morph.ProcessEngine.Services;
using Xunit;
using YamlDotNet.Core;

namespace Morph.ProcessEngine.Tests;

public sealed class ProcessDefinitionValidationTests
{
    [Fact]
    public void ParseYaml_rejects_malformed_yaml()
    {
        var act = () => ProcessIrSerializer.ParseYaml("steps: [\n  - id: broken");
        act.Should().Throw<YamlException>();
    }

    [Fact]
    public void Compile_rejects_process_with_no_steps()
    {
        var doc = ProcessIrSerializer.ParseYaml("""
            apiVersion: morph/v1
            kind: Process
            slug: empty
            steps: []
            """);

        var result = new ProcessCompiler().Compile(doc, Guid.NewGuid());
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("at least one step");
    }

    [Fact]
    public async Task ValidateAsync_fails_when_draft_contains_unknown_activity()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock, SystemClock>();
        services.AddMorphProcessEngine();
        await using var provider = services.BuildServiceProvider();

        var definitions = provider.GetRequiredService<ProcessDefinitionService>();
        var envId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var doc = ProcessIrSerializer.ParseYaml("""
            apiVersion: morph/v1
            kind: Process
            slug: bad
            steps:
              - id: x
                type: not.a.real.activity
            """);

        var upsert = await definitions.UpsertDraftAsync(envId, projectId, doc);
        upsert.IsSuccess.Should().BeTrue();

        var validation = await definitions.ValidateAsync(upsert.Value.Version.VersionId);
        validation.IsFailure.Should().BeTrue();
        validation.Error!.Message.Should().Contain("Unknown activity");
    }
}
