using Morph.Functions.Runtime.Domain;

namespace Morph.Functions.Runtime.Ports;

public interface IFunctionInvoker
{
    Task<FunctionInvokeResult> InvokeAsync(
        Guid environmentId,
        string slug,
        object? input,
        CancellationToken cancellationToken = default);
}
