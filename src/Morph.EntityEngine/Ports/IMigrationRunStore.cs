using Morph.EntityEngine.Domain;

namespace Morph.EntityEngine.Ports;

public interface IMigrationRunStore
{
    Task<MigrationRun?> GetAsync(Guid runId, CancellationToken cancellationToken = default);

    Task SaveAsync(MigrationRun run, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MigrationRun>> ListByDefinitionAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default);
}
