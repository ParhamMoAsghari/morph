using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Morph.Functions.Runtime.DependencyInjection;
using Morph.Functions.Runtime.Domain;
using Morph.Functions.Runtime.Services;
using Xunit;

namespace Morph.Functions.Tests;

public sealed class FunctionListAndDuplicateTests
{
    [Fact]
    public async Task ListAsync_returns_registered_functions_for_environment()
    {
        await using var provider = BuildProvider();
        var functions = provider.GetRequiredService<FunctionRuntimeService>();
        var envId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        (await functions.RegisterAsync(
            envId, projectId, "alpha", "Alpha", FunctionRuntimeKind.TypeScriptStub, FunctionTriggerKind.Http, null))
            .IsSuccess.Should().BeTrue();
        (await functions.RegisterAsync(
            envId, projectId, "beta", "Beta", FunctionRuntimeKind.DotNetStub, FunctionTriggerKind.Process, null))
            .IsSuccess.Should().BeTrue();

        var list = await functions.ListAsync(envId);
        list.Should().HaveCount(2);
        list.Select(f => f.Slug).Should().BeEquivalentTo(["alpha", "beta"]);
    }

    [Fact]
    public async Task Invoke_with_null_input_echoes_null()
    {
        await using var provider = BuildProvider();
        var functions = provider.GetRequiredService<FunctionRuntimeService>();
        var envId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        (await functions.RegisterAsync(
            envId, projectId, "noop", "Noop", FunctionRuntimeKind.TypeScriptStub, FunctionTriggerKind.Http, null))
            .IsSuccess.Should().BeTrue();

        var invoke = await functions.InvokeAsync(envId, "noop", null);
        invoke.IsSuccess.Should().BeTrue();
        var output = invoke.Value.Output as IDictionary<string, object?>;
        output.Should().NotBeNull();
        output!.Should().ContainKey("echoed");
        output["echoed"].Should().BeNull();
    }

    [Fact]
    public async Task Register_duplicate_slug_updates_same_function_id()
    {
        await using var provider = BuildProvider();
        var functions = provider.GetRequiredService<FunctionRuntimeService>();
        var envId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var first = await functions.RegisterAsync(
            envId, projectId, "hook", "Hook v1", FunctionRuntimeKind.TypeScriptStub, FunctionTriggerKind.Http, "v1");
        var second = await functions.RegisterAsync(
            envId, projectId, "hook", "Hook v2", FunctionRuntimeKind.DotNetStub, FunctionTriggerKind.Process, "v2");

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        second.Value.FunctionId.Should().Be(first.Value.FunctionId);
        second.Value.DisplayName.Should().Be("Hook v2");
        second.Value.Runtime.Should().Be(FunctionRuntimeKind.DotNetStub);
        second.Value.Source.Should().Be("v2");

        (await functions.ListAsync(envId)).Should().ContainSingle(f => f.Slug == "hook");
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddMorphFunctionsRuntime();
        return services.BuildServiceProvider();
    }
}
