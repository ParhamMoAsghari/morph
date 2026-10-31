using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Morph.Access.Domain;
using Morph.BuildingBlocks.Time;
using Morph.Functions.Runtime.DependencyInjection;
using Morph.Functions.Runtime.Domain;
using Morph.Functions.Runtime.Services;
using Morph.ProcessEngine.DependencyInjection;
using Morph.ProcessEngine.Domain;
using Morph.ProcessEngine.Execution;
using Morph.ProcessEngine.Ir;
using Morph.ProcessEngine.Ports;
using Morph.ProcessEngine.Services;
using Xunit;

namespace Morph.Functions.Tests;

public sealed class FunctionInvokeTests
{
    [Fact]
    public async Task Register_and_invoke_echoes_input()
    {
        await using var provider = BuildFunctionsProvider();
        var functions = provider.GetRequiredService<FunctionRuntimeService>();
        var envId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var registered = await functions.RegisterAsync(
            envId,
            projectId,
            "after-create",
            "After Create",
            FunctionRuntimeKind.TypeScriptStub,
            FunctionTriggerKind.Http,
            source: "export default async (ctx) => ctx.input");
        registered.IsSuccess.Should().BeTrue();

        var invoke = await functions.InvokeAsync(envId, "after-create", new { hello = "world" });
        invoke.IsSuccess.Should().BeTrue();
        invoke.Value.Succeeded.Should().BeTrue();

        var output = invoke.Value.Output as IDictionary<string, object?>;
        output.Should().NotBeNull();
        output!.Should().ContainKey("slug");
        output["slug"].Should().Be("after-create");
        output.Should().ContainKey("echoed");
        output.Should().ContainKey("runtime");
    }

    [Fact]
    public async Task Invoke_missing_function_fails()
    {
        await using var provider = BuildFunctionsProvider();
        var functions = provider.GetRequiredService<FunctionRuntimeService>();

        var invoke = await functions.InvokeAsync(Guid.NewGuid(), "missing", null);
        invoke.IsFailure.Should().BeTrue();
        invoke.Error!.Code.Should().Be("not_found");
    }

    [Fact]
    public async Task Process_InvokeFunction_activity_uses_adapter()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock, SystemClock>();
        services.AddLogging();
        services.AddMorphFunctionsRuntime();
        services.AddScoped<IProcessFunctionInvoker, ProcessFunctionInvokerForTests>();
        services.AddMorphProcessEngine();
        await using var provider = services.BuildServiceProvider();

        var functions = provider.GetRequiredService<FunctionRuntimeService>();
        var definitions = provider.GetRequiredService<ProcessDefinitionService>();
        var runtime = provider.GetRequiredService<ProcessRuntimeService>();

        var envId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await functions.RegisterAsync(
            envId,
            projectId,
            "hook",
            "Hook",
            FunctionRuntimeKind.DotNetStub,
            FunctionTriggerKind.Process,
            source: null);

        var doc = ProcessIrSerializer.ParseYaml("""
            apiVersion: morph/v1
            kind: Process
            slug: call-fn
            steps:
              - id: invoke
                type: morph.functions.invoke
                with:
                  slug: hook
                  input:
                    n: 1
                  resultVar: out
            """);

        var upsert = await definitions.UpsertDraftAsync(envId, projectId, doc);
        upsert.IsSuccess.Should().BeTrue();
        (await definitions.PublishAsync(upsert.Value.Version.VersionId)).IsSuccess.Should().BeTrue();

        var run = await runtime.StartAsync(
            envId,
            "call-fn",
            null,
            new AccessSubject { PrincipalId = "t", Roles = ["owner"] });
        run.IsSuccess.Should().BeTrue(run.IsFailure ? run.Error!.Message : null);
        run.Value.Status.Should().Be(ProcessInstanceStatus.Completed);

        var variables = VariableBag.FromJson(run.Value.VariablesJson);
        variables.TryGet("out", out var outValue).Should().BeTrue();
        var bag = outValue.Should().BeAssignableTo<IDictionary<string, object?>>().Subject;
        bag["slug"].Should().Be("hook");
    }

    private static ServiceProvider BuildFunctionsProvider()
    {
        var services = new ServiceCollection();
        services.AddMorphFunctionsRuntime();
        return services.BuildServiceProvider();
    }

    private sealed class ProcessFunctionInvokerForTests : IProcessFunctionInvoker
    {
        private readonly FunctionRuntimeService _functions;

        public ProcessFunctionInvokerForTests(FunctionRuntimeService functions) => _functions = functions;

        public async Task<object?> InvokeAsync(
            Guid environmentId,
            string slug,
            object? input,
            CancellationToken cancellationToken = default)
        {
            var result = await _functions.InvokeAsync(environmentId, slug, input, cancellationToken);
            if (result.IsFailure)
            {
                throw new InvalidOperationException(result.Error!.Message);
            }

            return result.Value.Output;
        }
    }
}
