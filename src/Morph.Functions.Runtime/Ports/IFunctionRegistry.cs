using Morph.Functions.Runtime.Domain;

namespace Morph.Functions.Runtime.Ports;

public interface IFunctionRegistry
{
    Task RegisterAsync(FunctionDefinition definition, CancellationToken cancellationToken = default);

    Task<FunctionDefinition?> GetBySlugAsync(
        Guid environmentId,
        string slug,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FunctionDefinition>> ListAsync(
        Guid environmentId,
        CancellationToken cancellationToken = default);
}
