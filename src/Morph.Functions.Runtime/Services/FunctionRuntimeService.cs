using Morph.BuildingBlocks.Results;
using Morph.BuildingBlocks.Time;
using Morph.Functions.Runtime.Domain;
using Morph.Functions.Runtime.Ports;

namespace Morph.Functions.Runtime.Services;

public sealed class FunctionRuntimeService
{
    private readonly IFunctionRegistry _registry;
    private readonly IFunctionInvoker _invoker;
    private readonly IClock _clock;

    public FunctionRuntimeService(
        IFunctionRegistry registry,
        IFunctionInvoker invoker,
        IClock clock)
    {
        _registry = registry;
        _invoker = invoker;
        _clock = clock;
    }

    public async Task<Result<FunctionDefinition>> RegisterAsync(
        Guid environmentId,
        Guid projectId,
        string slug,
        string? displayName,
        FunctionRuntimeKind runtime,
        FunctionTriggerKind trigger,
        string? source,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return Result.Failure<FunctionDefinition>(Error.Validation("Function slug is required."));
        }

        var normalized = slug.Trim().ToLowerInvariant();
        var now = _clock.UtcNow;
        var existing = await _registry
            .GetBySlugAsync(environmentId, normalized, cancellationToken)
            .ConfigureAwait(false);

        var definition = existing ?? new FunctionDefinition
        {
            FunctionId = Guid.NewGuid(),
            EnvironmentId = environmentId,
            ProjectId = projectId,
            Slug = normalized,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? normalized : displayName.Trim(),
            Runtime = runtime,
            Trigger = trigger,
            Source = source,
            CreatedAt = now,
            UpdatedAt = now
        };

        if (existing is not null)
        {
            definition.DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? existing.DisplayName
                : displayName.Trim();
            definition.Runtime = runtime;
            definition.Trigger = trigger;
            definition.Source = source;
            definition.UpdatedAt = now;
        }

        await _registry.RegisterAsync(definition, cancellationToken).ConfigureAwait(false);
        return Result.Success(definition);
    }

    public Task<IReadOnlyList<FunctionDefinition>> ListAsync(
        Guid environmentId,
        CancellationToken cancellationToken = default) =>
        _registry.ListAsync(environmentId, cancellationToken);

    public Task<FunctionDefinition?> GetBySlugAsync(
        Guid environmentId,
        string slug,
        CancellationToken cancellationToken = default) =>
        _registry.GetBySlugAsync(environmentId, slug, cancellationToken);

    public async Task<Result<FunctionInvokeResult>> InvokeAsync(
        Guid environmentId,
        string slug,
        object? input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return Result.Failure<FunctionInvokeResult>(Error.Validation("Function slug is required."));
        }

        var result = await _invoker
            .InvokeAsync(environmentId, slug, input, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return Result.Failure<FunctionInvokeResult>(
                Error.NotFound(result.Error ?? "Function invoke failed."));
        }

        return Result.Success(result);
    }
}
