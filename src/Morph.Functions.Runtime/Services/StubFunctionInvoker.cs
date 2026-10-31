using System.Diagnostics;
using Morph.Functions.Runtime.Domain;
using Morph.Functions.Runtime.Ports;

namespace Morph.Functions.Runtime.Services;

/// <summary>
/// In-process stub invoker — echoes input; no sandbox execution yet.
/// </summary>
public sealed class StubFunctionInvoker : IFunctionInvoker
{
    private readonly IFunctionRegistry _registry;

    public StubFunctionInvoker(IFunctionRegistry registry)
    {
        _registry = registry;
    }

    public async Task<FunctionInvokeResult> InvokeAsync(
        Guid environmentId,
        string slug,
        object? input,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var normalized = slug.Trim().ToLowerInvariant();
        var definition = await _registry
            .GetBySlugAsync(environmentId, normalized, cancellationToken)
            .ConfigureAwait(false);

        if (definition is null)
        {
            sw.Stop();
            return new FunctionInvokeResult
            {
                FunctionSlug = normalized,
                Succeeded = false,
                Error = $"Function '{normalized}' was not found in environment '{environmentId:D}'.",
                Duration = sw.Elapsed
            };
        }

        sw.Stop();
        return new FunctionInvokeResult
        {
            FunctionSlug = definition.Slug,
            Succeeded = true,
            Output = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["echoed"] = input,
                ["slug"] = definition.Slug,
                ["runtime"] = definition.Runtime.ToString()
            },
            Duration = sw.Elapsed
        };
    }
}
