using Microsoft.EntityFrameworkCore;
using Morph.EntityEngine.Domain;
using Morph.EntityEngine.Ports;

namespace Morph.Infrastructure.Persistence.EntityEngine;

public sealed class EfMigrationRunStore : IMigrationRunStore
{
    private readonly PlatformDbContext _db;

    public EfMigrationRunStore(PlatformDbContext db)
    {
        _db = db;
    }

    public async Task<MigrationRun?> GetAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var record = await _db.MigrationRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RunId == runId, cancellationToken)
            .ConfigureAwait(false);
        return record is null ? null : ToDomain(record);
    }

    public async Task SaveAsync(MigrationRun run, CancellationToken cancellationToken = default)
    {
        var existing = await _db.MigrationRuns
            .FirstOrDefaultAsync(r => r.RunId == run.RunId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            _db.MigrationRuns.Add(ToRecord(run));
        }
        else
        {
            existing.Status = run.Status.ToString();
            existing.CurrentStage = run.CurrentStage.ToString();
            existing.LastCompletedStepOrdinal = run.LastCompletedStepOrdinal;
            existing.FailureMessage = run.FailureMessage;
            existing.FailureStage = run.FailureStage;
            existing.PlanJson = run.PlanJson;
            existing.PriorPublishedVersionId = run.PriorPublishedVersionId;
            existing.StartedAt = run.StartedAt;
            existing.CompletedAt = run.CompletedAt;
            existing.FromVersionId = run.FromVersionId;
            existing.ToVersionId = run.ToVersionId;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MigrationRun>> ListByDefinitionAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default)
    {
        var records = await _db.MigrationRuns
            .AsNoTracking()
            .Where(r => r.DefinitionId == definitionId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return records.Select(ToDomain).ToList();
    }

    private static MigrationRunRecord ToRecord(MigrationRun run) => new()
    {
        RunId = run.RunId,
        PlanId = run.PlanId,
        EnvironmentId = run.EnvironmentId,
        DefinitionId = run.DefinitionId,
        FromVersionId = run.FromVersionId,
        ToVersionId = run.ToVersionId,
        Status = run.Status.ToString(),
        CurrentStage = run.CurrentStage.ToString(),
        LastCompletedStepOrdinal = run.LastCompletedStepOrdinal,
        FailureMessage = run.FailureMessage,
        FailureStage = run.FailureStage,
        PlanJson = run.PlanJson,
        PriorPublishedVersionId = run.PriorPublishedVersionId,
        CreatedAt = run.CreatedAt,
        StartedAt = run.StartedAt,
        CompletedAt = run.CompletedAt
    };

    private static MigrationRun ToDomain(MigrationRunRecord record) => new()
    {
        RunId = record.RunId,
        PlanId = record.PlanId,
        EnvironmentId = record.EnvironmentId,
        DefinitionId = record.DefinitionId,
        FromVersionId = record.FromVersionId,
        ToVersionId = record.ToVersionId,
        Status = Enum.Parse<MigrationRunStatus>(record.Status, ignoreCase: true),
        CurrentStage = Enum.Parse<MigrationStage>(record.CurrentStage, ignoreCase: true),
        LastCompletedStepOrdinal = record.LastCompletedStepOrdinal,
        FailureMessage = record.FailureMessage,
        FailureStage = record.FailureStage,
        PlanJson = record.PlanJson,
        PriorPublishedVersionId = record.PriorPublishedVersionId,
        CreatedAt = record.CreatedAt,
        StartedAt = record.StartedAt,
        CompletedAt = record.CompletedAt
    };
}
