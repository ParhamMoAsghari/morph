using System.Text.Json;
using Morph.Access.Domain;
using Morph.BuildingBlocks.Results;
using Morph.BuildingBlocks.Time;
using Morph.ProcessEngine.Domain;
using Morph.ProcessEngine.Execution;
using Morph.ProcessEngine.Ir;
using Morph.ProcessEngine.Ports;

namespace Morph.ProcessEngine.Services;

public sealed class ProcessRuntimeService
{
    private readonly IProcessDefinitionStore _definitions;
    private readonly IProcessInstanceStore _instances;
    private readonly ProcessRunner _runner;
    private readonly IClock _clock;

    public ProcessRuntimeService(
        IProcessDefinitionStore definitions,
        IProcessInstanceStore instances,
        ProcessRunner runner,
        IClock clock)
    {
        _definitions = definitions;
        _instances = instances;
        _runner = runner;
        _clock = clock;
    }

    public async Task<Result<ProcessInstance>> StartAsync(
        Guid environmentId,
        string slug,
        IReadOnlyDictionary<string, object?>? input,
        AccessSubject subject,
        CancellationToken cancellationToken = default)
    {
        var definition = await _definitions.GetDefinitionBySlugAsync(environmentId, slug, cancellationToken)
            .ConfigureAwait(false);
        if (definition is null || definition.CurrentPublishedVersionId is null)
        {
            return Result.Failure<ProcessInstance>(
                Error.NotFound($"Published process '{slug}' was not found."));
        }

        var version = await _definitions.GetVersionAsync(definition.CurrentPublishedVersionId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (version is null || string.IsNullOrWhiteSpace(version.CompiledJson))
        {
            return Result.Failure<ProcessInstance>(Error.Conflict("Process version is not compiled."));
        }

        var compiled = ProcessIrSerializer.ParseCompiled(version.CompiledJson);
        var variables = new VariableBag();
        foreach (var decl in compiled.Variables)
        {
            if (decl.Default.HasValue)
            {
                variables.Set(decl.Name, variables.Resolve(decl.Default.Value));
            }
        }

        if (input is not null)
        {
            foreach (var (k, v) in input)
            {
                variables.Set(k, v);
            }
        }

        // Nest inputs under "input" for {{input.x}} paths.
        variables.Set("input", input is null
            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object?>(input, StringComparer.OrdinalIgnoreCase));

        var now = _clock.UtcNow;
        var instance = new ProcessInstance
        {
            InstanceId = Guid.NewGuid(),
            DefinitionId = definition.DefinitionId,
            VersionId = version.VersionId,
            EnvironmentId = environmentId,
            ProjectId = definition.ProjectId,
            Slug = definition.Slug,
            Status = ProcessInstanceStatus.Running,
            VariablesJson = variables.ToJson(),
            PrincipalId = subject.PrincipalId,
            PrincipalRolesJson = JsonSerializer.Serialize(subject.Roles),
            CreatedAt = now,
            UpdatedAt = now
        };

        await _instances.SaveAsync(instance, cancellationToken).ConfigureAwait(false);
        await _runner.RunAsync(instance, compiled, cancellationToken).ConfigureAwait(false);

        var fresh = await _instances.GetAsync(instance.InstanceId, cancellationToken).ConfigureAwait(false);
        return Result.Success(fresh ?? instance);
    }

    public Task<ProcessInstance?> GetInstanceAsync(Guid instanceId, CancellationToken cancellationToken = default) =>
        _instances.GetAsync(instanceId, cancellationToken);

    public Task<IReadOnlyList<ProcessActivityLogEntry>> GetLogsAsync(
        Guid instanceId,
        CancellationToken cancellationToken = default) =>
        _instances.ListLogsAsync(instanceId, cancellationToken);

    public async Task ResumeDueAsync(CancellationToken cancellationToken = default)
    {
        var due = await _instances.ListWaitingDueAsync(_clock.UtcNow, take: 50, cancellationToken)
            .ConfigureAwait(false);
        foreach (var instance in due)
        {
            var version = await _definitions.GetVersionAsync(instance.VersionId, cancellationToken)
                .ConfigureAwait(false);
            if (version?.CompiledJson is null)
            {
                continue;
            }

            var compiled = ProcessIrSerializer.ParseCompiled(version.CompiledJson);
            await _runner.RunAsync(instance, compiled, cancellationToken).ConfigureAwait(false);
        }
    }
}
