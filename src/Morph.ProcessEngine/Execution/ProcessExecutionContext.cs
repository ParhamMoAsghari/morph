using System.Text.Json;
using Morph.Access.Domain;
using Morph.DataPlane.Services;
using Morph.Plugins.Abstractions;
using Morph.ProcessEngine.Domain;
using Morph.ProcessEngine.Ir;

namespace Morph.ProcessEngine.Execution;

public sealed class ProcessExecutionContext : IProcessActivityContext
{
    public ProcessExecutionContext(
        ProcessInstance instance,
        CompiledProcess compiled,
        VariableBag variables,
        AccessSubject subject,
        IServiceProvider services,
        IPluginHttpClientFactory http)
    {
        Instance = instance;
        Compiled = compiled;
        Variables = variables;
        Subject = subject;
        Services = services;
        Http = http;
    }

    public ProcessInstance Instance { get; }

    public CompiledProcess Compiled { get; }

    public VariableBag Variables { get; }

    public AccessSubject Subject { get; }

    public IServiceProvider Services { get; }

    public IPluginHttpClientFactory Http { get; }

    IReadOnlyDictionary<string, object?> IProcessActivityContext.Variables => Variables.Snapshot;

    public void SetVariable(string name, object? value) => Variables.Set(name, value);

    public T GetRequiredService<T>() where T : notnull =>
        (T)(Services.GetService(typeof(T))
            ?? throw new InvalidOperationException($"Service {typeof(T).Name} is not registered."));

    public DataPlaneService DataPlane => GetRequiredService<DataPlaneService>();

    public Dictionary<string, JsonElement>? GetWith(CompiledStep step) => step.With;
}
