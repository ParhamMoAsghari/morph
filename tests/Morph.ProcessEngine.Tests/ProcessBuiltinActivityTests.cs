using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Morph.Access.Domain;
using Morph.BuildingBlocks.Time;
using Morph.ProcessEngine.DependencyInjection;
using Morph.ProcessEngine.Domain;
using Morph.ProcessEngine.Execution;
using Morph.ProcessEngine.Ir;
using Morph.ProcessEngine.Ports;
using Morph.ProcessEngine.Services;
using Morph.Plugins.Abstractions;
using Xunit;

namespace Morph.ProcessEngine.Tests;

public sealed class ProcessBuiltinActivityTests
{
    [Fact]
    public async Task Json_activities_parse_stringify_get_and_merge()
    {
        await using var provider = BuildProvider();
        var definitions = provider.GetRequiredService<ProcessDefinitionService>();
        var runtime = provider.GetRequiredService<ProcessRuntimeService>();

        var envId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var doc = ProcessIrSerializer.ParseYaml("""
            apiVersion: morph/v1
            kind: Process
            slug: json-flow
            steps:
              - id: parse
                type: json.parse
                with:
                  input: '{"name":"morph","n":2}'
                  resultVar: doc
              - id: get
                type: json.get
                with:
                  path: doc.name
                  resultVar: name
              - id: set
                type: json.set
                with:
                  target: doc
                  path: extra
                  value: ok
              - id: merge
                type: json.merge
                with:
                  target: doc
                  source:
                    flag: true
              - id: out
                type: json.stringify
                with:
                  value: "{{name}}"
                  resultVar: serialized
            """);

        await PublishAndRun(definitions, runtime, envId, projectId, doc);

        var instance = (await runtime.StartAsync(
            envId,
            "json-flow",
            null,
            Subject())).Value;

        instance.Status.Should().Be(ProcessInstanceStatus.Completed);
        var vars = VariableBag.FromJson(instance.VariablesJson);
        vars.TryGet("name", out var name).Should().BeTrue();
        name.Should().Be("morph");
        vars.TryGet("serialized", out var serialized).Should().BeTrue();
        serialized.Should().BeOfType<string>().Which.Should().Be("\"morph\"");
    }

    [Fact]
    public async Task If_activity_executes_then_branch_when_condition_truthy()
    {
        await using var provider = BuildProvider();
        var definitions = provider.GetRequiredService<ProcessDefinitionService>();
        var runtime = provider.GetRequiredService<ProcessRuntimeService>();

        var envId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var doc = ProcessIrSerializer.ParseYaml("""
            apiVersion: morph/v1
            kind: Process
            slug: branch
            steps:
              - id: seed
                type: variables.set
                with:
                  name: flag
                  value: yes
              - id: gate
                type: if
                with:
                  condition: "{{flag}} == yes"
                then:
                  - id: then-set
                    type: variables.set
                    with:
                      name: branch
                      value: then
                else:
                  - id: else-set
                    type: variables.set
                    with:
                      name: branch
                      value: else
            """);

        await PublishAndRun(definitions, runtime, envId, projectId, doc);

        var run = await runtime.StartAsync(envId, "branch", null, Subject());
        run.Value.Status.Should().Be(ProcessInstanceStatus.Completed);

        VariableBag.FromJson(run.Value.VariablesJson).TryGet("branch", out var branch).Should().BeTrue();
        branch.Should().Be("then");
    }

    [Fact]
    public async Task String_replace_and_trim_activities_run()
    {
        await using var provider = BuildProvider();
        var definitions = provider.GetRequiredService<ProcessDefinitionService>();
        var runtime = provider.GetRequiredService<ProcessRuntimeService>();

        var envId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var doc = ProcessIrSerializer.ParseYaml("""
            apiVersion: morph/v1
            kind: Process
            slug: strings
            steps:
              - id: trim
                type: string.trim
                with:
                  input: "  hi  "
                  resultVar: trimmed
              - id: replace
                type: string.replace
                with:
                  input: "{{trimmed}}"
                  old: hi
                  new: morph
                  resultVar: out
            """);

        await PublishAndRun(definitions, runtime, envId, projectId, doc);

        var run = await runtime.StartAsync(envId, "strings", null, Subject());
        VariableBag.FromJson(run.Value.VariablesJson).TryGet("out", out var value).Should().BeTrue();
        value.Should().Be("morph");
    }

    [Fact]
    public async Task Http_request_activity_uses_injected_client()
    {
        await using var provider = BuildProvider(withHttp: true);
        var definitions = provider.GetRequiredService<ProcessDefinitionService>();
        var runtime = provider.GetRequiredService<ProcessRuntimeService>();

        var envId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var doc = ProcessIrSerializer.ParseYaml("""
            apiVersion: morph/v1
            kind: Process
            slug: http-get
            steps:
              - id: call
                type: http.request
                with:
                  method: GET
                  url: https://example.com/api
                  resultVar: http
            """);

        await PublishAndRun(definitions, runtime, envId, projectId, doc);

        var run = await runtime.StartAsync(envId, "http-get", null, Subject());
        run.Value.Status.Should().Be(ProcessInstanceStatus.Completed);

        var vars = VariableBag.FromJson(run.Value.VariablesJson);
        vars.TryGet("http", out var httpObj).Should().BeTrue();
        httpObj.Should().BeAssignableTo<IDictionary<string, object?>>();
        var http = (IDictionary<string, object?>)httpObj!;
        http["statusCode"].Should().Be(200);
    }

    [Fact]
    public async Task Invoke_function_activity_uses_registered_invoker()
    {
        await using var provider = BuildProvider(withFunctionInvoker: true);
        var definitions = provider.GetRequiredService<ProcessDefinitionService>();
        var runtime = provider.GetRequiredService<ProcessRuntimeService>();

        var envId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var doc = ProcessIrSerializer.ParseYaml("""
            apiVersion: morph/v1
            kind: Process
            slug: fn
            steps:
              - id: call
                type: morph.functions.invoke
                with:
                  slug: echo
                  input: { "x": 1 }
                  resultVar: functionResult
            """);

        await PublishAndRun(definitions, runtime, envId, projectId, doc);

        var run = await runtime.StartAsync(envId, "fn", null, Subject());
        VariableBag.FromJson(run.Value.VariablesJson).TryGet("functionResult", out var result).Should().BeTrue();
        result.Should().Be("echo:1");
    }

    [Fact]
    public async Task ResumeDueAsync_completes_waiting_delay_after_due_time()
    {
        await using var provider = BuildProvider();
        var definitions = provider.GetRequiredService<ProcessDefinitionService>();
        var runtime = provider.GetRequiredService<ProcessRuntimeService>();
        var instances = provider.GetRequiredService<IProcessInstanceStore>();

        var envId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var doc = ProcessIrSerializer.ParseYaml("""
            apiVersion: morph/v1
            kind: Process
            slug: resume-me
            steps:
              - id: pause
                type: delay
                with:
                  seconds: 30
            """);

        await PublishAndRun(definitions, runtime, envId, projectId, doc);

        var run = await runtime.StartAsync(envId, "resume-me", null, Subject());
        run.Value.Status.Should().Be(ProcessInstanceStatus.Waiting);

        var waiting = await runtime.GetInstanceAsync(run.Value.InstanceId);
        waiting!.WaitingUntil = DateTimeOffset.UtcNow.AddSeconds(-5);
        await instances.SaveAsync(waiting);

        await runtime.ResumeDueAsync();

        var completed = await runtime.GetInstanceAsync(run.Value.InstanceId);
        completed!.Status.Should().Be(ProcessInstanceStatus.Completed);
    }

    private static async Task PublishAndRun(
        ProcessDefinitionService definitions,
        ProcessRuntimeService runtime,
        Guid envId,
        Guid projectId,
        ProcessDocument doc)
    {
        var upsert = await definitions.UpsertDraftAsync(envId, projectId, doc);
        upsert.IsSuccess.Should().BeTrue();
        (await definitions.PublishAsync(upsert.Value.Version.VersionId)).IsSuccess.Should().BeTrue();
    }

    private static AccessSubject Subject() =>
        new() { PrincipalId = "test", Roles = ["owner"] };

    private static ServiceProvider BuildProvider(bool withHttp = false, bool withFunctionInvoker = false)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock, SystemClock>();
        services.AddMorphProcessEngine();
        services.AddLogging();

        if (withHttp)
        {
            services.RemoveAll<IPluginHttpClientFactory>();
            services.AddSingleton<IPluginHttpClientFactory, StubHttpClientFactory>();
        }

        if (withFunctionInvoker)
        {
            services.RemoveAll<IProcessFunctionInvoker>();
            services.AddSingleton<IProcessFunctionInvoker, StubFunctionInvoker>();
        }

        return services.BuildServiceProvider();
    }

    private sealed class StubHttpClientFactory : IPluginHttpClientFactory
    {
        public HttpClient CreateClient(string? name = null) =>
            new(new StubHandler());
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"ok":true}""", Encoding.UTF8, "application/json")
            });
    }

    private sealed class StubFunctionInvoker : IProcessFunctionInvoker
    {
        public Task<object?> InvokeAsync(
            Guid environmentId,
            string slug,
            object? input,
            CancellationToken cancellationToken = default)
        {
            var x = input is IDictionary<string, object?> dict && dict.TryGetValue("x", out var v) ? v : input;
            return Task.FromResult<object?>(slug + ":" + x);
        }
    }
}
