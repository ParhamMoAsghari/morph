using System.Collections.Concurrent;
using Morph.EntityEngine.Domain;
using Morph.EntityEngine.Ports;

namespace Morph.EntityEngine.Persistence;

public sealed class InMemoryMigrationRunStore : IMigrationRunStore
{
    private readonly ConcurrentDictionary<Guid, MigrationRun> _runs = new();

    public Task<MigrationRun?> GetAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        _runs.TryGetValue(runId, out var run);
        return Task.FromResult(run);
    }

    public Task SaveAsync(MigrationRun run, CancellationToken cancellationToken = default)
    {
        _runs[run.RunId] = run;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MigrationRun>> ListByDefinitionAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<MigrationRun> list = _runs.Values
            .Where(r => r.DefinitionId == definitionId)
            .OrderBy(r => r.CreatedAt)
            .ToList();
        return Task.FromResult(list);
    }
}
