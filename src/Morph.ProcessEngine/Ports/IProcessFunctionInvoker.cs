namespace Morph.ProcessEngine.Ports;

/// <summary>
/// Thin port so ProcessEngine can invoke Morph Functions without referencing Functions.Runtime.
/// </summary>
public interface IProcessFunctionInvoker
{
    Task<object?> InvokeAsync(
        Guid environmentId,
        string slug,
        object? input,
        CancellationToken cancellationToken = default);
}
