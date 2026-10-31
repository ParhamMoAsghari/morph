namespace Morph.ProcessEngine.Ports;

/// <summary>
/// Default when Functions runtime is not wired into the host.
/// </summary>
public sealed class NullProcessFunctionInvoker : IProcessFunctionInvoker
{
    public Task<object?> InvokeAsync(
        Guid environmentId,
        string slug,
        object? input,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(
            "Functions runtime is not configured. Register IProcessFunctionInvoker (e.g. via AddMorphFunctionsRuntime).");
}
