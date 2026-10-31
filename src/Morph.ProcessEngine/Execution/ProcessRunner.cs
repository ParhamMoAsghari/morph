using System.Text.Json;
using Morph.Access.Domain;
using Morph.Plugins.Abstractions;
using Morph.ProcessEngine.Activities;
using Morph.ProcessEngine.Domain;
using Morph.ProcessEngine.Ir;
using Morph.ProcessEngine.Ports;
using Microsoft.Extensions.Logging;

namespace Morph.ProcessEngine.Execution;

public sealed class ProcessRunner
{
    private readonly BuiltinActivityCatalog _builtins;
    private readonly IProcessActivityRegistry _pluginRegistry;
    private readonly IProcessInstanceStore _instances;
    private readonly IPluginHttpClientFactory _http;
    private readonly IServiceProvider _services;
    private readonly ILogger<ProcessRunner> _logger;

    public ProcessRunner(
        BuiltinActivityCatalog builtins,
        IProcessActivityRegistry pluginRegistry,
        IProcessInstanceStore instances,
        IPluginHttpClientFactory http,
        IServiceProvider services,
        ILogger<ProcessRunner> logger)
    {
        _builtins = builtins;
        _pluginRegistry = pluginRegistry;
        _instances = instances;
        _http = http;
        _services = services;
        _logger = logger;
    }

    public async Task RunAsync(ProcessInstance instance, CompiledProcess compiled, CancellationToken cancellationToken)
    {
        var variables = VariableBag.FromJson(instance.VariablesJson);
        var subject = BuildSubject(instance);
        var context = new ProcessExecutionContext(instance, compiled, variables, subject, _services, _http);

        var cursor = DeserializeCursor(instance.CursorJson);
        try
        {
            instance.Status = ProcessInstanceStatus.Running;
            instance.WaitingUntil = null;
            instance.UpdatedAt = DateTimeOffset.UtcNow;
            await _instances.SaveAsync(instance, cancellationToken).ConfigureAwait(false);

            await ExecuteStepsAsync(compiled.Steps, context, cursor, cancellationToken).ConfigureAwait(false);

            if (instance.Status == ProcessInstanceStatus.Waiting)
            {
                instance.VariablesJson = variables.ToJson();
                instance.CursorJson = SerializeCursor(cursor);
                instance.UpdatedAt = DateTimeOffset.UtcNow;
                await _instances.SaveAsync(instance, cancellationToken).ConfigureAwait(false);
                return;
            }

            instance.Status = ProcessInstanceStatus.Completed;
            instance.CompletedAt = DateTimeOffset.UtcNow;
            instance.VariablesJson = variables.ToJson();
            instance.CursorJson = null;
            instance.UpdatedAt = DateTimeOffset.UtcNow;
            await _instances.SaveAsync(instance, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Process instance {InstanceId} failed", instance.InstanceId);
            instance.Status = ProcessInstanceStatus.Failed;
            instance.FailureMessage = ex.Message;
            instance.VariablesJson = variables.ToJson();
            instance.CursorJson = SerializeCursor(cursor);
            instance.UpdatedAt = DateTimeOffset.UtcNow;
            instance.CompletedAt = DateTimeOffset.UtcNow;
            await _instances.SaveAsync(instance, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ExecuteStepsAsync(
        IReadOnlyList<CompiledStep> steps,
        ProcessExecutionContext context,
        RunCursor cursor,
        CancellationToken cancellationToken)
    {
        var start = cursor.Index;
        for (var i = start; i < steps.Count; i++)
        {
            cursor.Index = i;
            var step = steps[i];

            if (string.Equals(step.Type, ActivityTypes.Sequence, StringComparison.OrdinalIgnoreCase))
            {
                var child = cursor.EnterChild(step.Id);
                await ExecuteStepsAsync(step.Steps ?? [], context, child, cancellationToken).ConfigureAwait(false);
                if (context.Instance.Status == ProcessInstanceStatus.Waiting)
                {
                    return;
                }

                cursor.ExitChild();
                continue;
            }

            if (string.Equals(step.Type, ActivityTypes.If, StringComparison.OrdinalIgnoreCase))
            {
                var condition = step.With is not null && step.With.TryGetValue("condition", out var condEl)
                    ? context.Variables.Resolve(condEl)?.ToString() ?? ""
                    : "";
                var branch = context.Variables.EvaluateCondition(condition) ? step.Then : step.Else;
                var child = cursor.EnterChild(step.Id);
                await ExecuteStepsAsync(branch ?? [], context, child, cancellationToken).ConfigureAwait(false);
                if (context.Instance.Status == ProcessInstanceStatus.Waiting)
                {
                    return;
                }

                cursor.ExitChild();
                continue;
            }

            if (string.Equals(step.Type, ActivityTypes.Delay, StringComparison.OrdinalIgnoreCase))
            {
                if (cursor.CompletedStepIds.Contains(step.Id))
                {
                    continue;
                }

                var delay = DelayActivity.ResolveDelay(step, context);
                if (delay <= TimeSpan.FromSeconds(2))
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    cursor.CompletedStepIds.Add(step.Id);
                    await LogAsync(context.Instance.InstanceId, step, "completed", null, null, cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                context.Instance.Status = ProcessInstanceStatus.Waiting;
                context.Instance.WaitingUntil = DateTimeOffset.UtcNow.Add(delay);
                cursor.CompletedStepIds.Add(step.Id); // resume past this delay
                await LogAsync(context.Instance.InstanceId, step, "waiting", null, null, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            if (cursor.CompletedStepIds.Contains(step.Id))
            {
                continue;
            }

            await ExecuteLeafAsync(step, context, cancellationToken).ConfigureAwait(false);
            cursor.CompletedStepIds.Add(step.Id);
        }

        cursor.Index = steps.Count;
    }

    private async Task ExecuteLeafAsync(
        CompiledStep step,
        ProcessExecutionContext context,
        CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        try
        {
            if (_builtins.TryGet(step.Type, out var builtin))
            {
                await builtin.ExecuteAsync(step, context, cancellationToken).ConfigureAwait(false);
            }
            else if (_pluginRegistry.TryGet(step.Type, out var descriptor))
            {
                var activity = (IProcessActivity)(Activator.CreateInstance(descriptor.ActivityType)
                    ?? throw new InvalidOperationException($"Could not create plugin activity '{step.Type}'."));
                await activity.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new InvalidOperationException($"No activity registered for type '{step.Type}'.");
            }

            await LogAsync(context.Instance.InstanceId, step, "completed", null, null, cancellationToken, started)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await LogAsync(context.Instance.InstanceId, step, "failed", null, ex.Message, cancellationToken, started)
                .ConfigureAwait(false);
            throw;
        }
    }

    private Task LogAsync(
        Guid instanceId,
        CompiledStep step,
        string status,
        string? input,
        string? error,
        CancellationToken cancellationToken,
        DateTimeOffset? started = null) =>
        _instances.AppendLogAsync(
            new ProcessActivityLogEntry
            {
                LogId = Guid.NewGuid(),
                InstanceId = instanceId,
                StepId = step.Id,
                ActivityType = step.Type,
                Status = status,
                InputJson = input,
                ErrorMessage = error,
                StartedAt = started ?? DateTimeOffset.UtcNow,
                CompletedAt = DateTimeOffset.UtcNow
            },
            cancellationToken);

    private static AccessSubject BuildSubject(ProcessInstance instance)
    {
        var roles = Array.Empty<string>();
        if (!string.IsNullOrWhiteSpace(instance.PrincipalRolesJson))
        {
            roles = JsonSerializer.Deserialize<string[]>(instance.PrincipalRolesJson) ?? [];
        }

        return new AccessSubject
        {
            PrincipalId = instance.PrincipalId ?? "process",
            Roles = roles,
            Attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["sub"] = instance.PrincipalId ?? "process",
                ["process_instance_id"] = instance.InstanceId.ToString("D")
            }
        };
    }

    private static RunCursor DeserializeCursor(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new RunCursor();
        }

        return JsonSerializer.Deserialize<RunCursor>(json, ProcessIrSerializer.JsonOptions) ?? new RunCursor();
    }

    private static string SerializeCursor(RunCursor cursor) =>
        JsonSerializer.Serialize(cursor, ProcessIrSerializer.JsonOptions);
}

public sealed class RunCursor
{
    public int Index { get; set; }

    public List<string> CompletedStepIds { get; set; } = [];

    public Dictionary<string, RunCursor> Children { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public RunCursor EnterChild(string stepId)
    {
        if (!Children.TryGetValue(stepId, out var child))
        {
            child = new RunCursor();
            Children[stepId] = child;
        }

        return child;
    }

    public void ExitChild()
    {
        // retained in Children for resume
    }
}
