using Morph.Functions.Runtime.Services;
using Morph.ProcessEngine.Ports;

namespace Morph.Infrastructure.Functions;

/// <summary>
/// Bridges ProcessEngine's <see cref="IProcessFunctionInvoker"/> to Morph.Functions.Runtime.
/// </summary>
public sealed class ProcessFunctionInvokerAdapter : IProcessFunctionInvoker
{
    private readonly FunctionRuntimeService _functions;

    public ProcessFunctionInvokerAdapter(FunctionRuntimeService functions)
    {
        _functions = functions;
    }

    public async Task<object?> InvokeAsync(
        Guid environmentId,
        string slug,
        object? input,
        CancellationToken cancellationToken = default)
    {
        var result = await _functions
            .InvokeAsync(environmentId, slug, input, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error!.Message);
        }

        return result.Value.Output;
    }
}
