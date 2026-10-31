using Microsoft.EntityFrameworkCore;
using Morph.ProcessEngine.Domain;
using Morph.ProcessEngine.Ports;

namespace Morph.Infrastructure.Persistence.ProcessEngine;

public sealed class EfProcessDefinitionStore : IProcessDefinitionStore
{
    private readonly PlatformDbContext _db;

    public EfProcessDefinitionStore(PlatformDbContext db) => _db = db;

    public async Task<ProcessDefinition?> GetDefinitionAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.ProcessDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DefinitionId == definitionId, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : MapDefinition(row);
    }

    public async Task<ProcessDefinition?> GetDefinitionBySlugAsync(
        Guid environmentId,
        string slug,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.ProcessDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.EnvironmentId == environmentId && x.Slug == slug.ToLowerInvariant(),
                cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : MapDefinition(row);
    }

    public async Task<IReadOnlyList<ProcessDefinition>> ListDefinitionsAsync(
        Guid environmentId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.ProcessDefinitions.AsNoTracking()
            .Where(x => x.EnvironmentId == environmentId)
            .OrderBy(x => x.Slug)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(MapDefinition).ToList();
    }

    public async Task<ProcessDefinitionVersion?> GetVersionAsync(
        Guid versionId,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.ProcessVersions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.VersionId == versionId, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : MapVersion(row);
    }

    public async Task<IReadOnlyList<ProcessDefinitionVersion>> ListVersionsAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.ProcessVersions.AsNoTracking()
            .Where(x => x.DefinitionId == definitionId)
            .OrderBy(x => x.VersionNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(MapVersion).ToList();
    }

    public async Task SaveDefinitionAsync(ProcessDefinition definition, CancellationToken cancellationToken = default)
    {
        var row = await _db.ProcessDefinitions
            .FirstOrDefaultAsync(x => x.DefinitionId == definition.DefinitionId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            _db.ProcessDefinitions.Add(new ProcessDefinitionRecord
            {
                DefinitionId = definition.DefinitionId,
                EnvironmentId = definition.EnvironmentId,
                ProjectId = definition.ProjectId,
                Slug = definition.Slug.ToLowerInvariant(),
                DisplayName = definition.DisplayName,
                CurrentPublishedVersionId = definition.CurrentPublishedVersionId,
                CreatedAt = definition.CreatedAt,
                UpdatedAt = definition.UpdatedAt
            });
        }
        else
        {
            row.DisplayName = definition.DisplayName;
            row.CurrentPublishedVersionId = definition.CurrentPublishedVersionId;
            row.UpdatedAt = definition.UpdatedAt;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveVersionAsync(ProcessDefinitionVersion version, CancellationToken cancellationToken = default)
    {
        var row = await _db.ProcessVersions
            .FirstOrDefaultAsync(x => x.VersionId == version.VersionId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            _db.ProcessVersions.Add(new ProcessVersionRecord
            {
                VersionId = version.VersionId,
                DefinitionId = version.DefinitionId,
                EnvironmentId = version.EnvironmentId,
                Slug = version.Slug.ToLowerInvariant(),
                DisplayName = version.DisplayName,
                VersionNumber = version.VersionNumber,
                Status = version.Status.ToString(),
                AuthoringJson = version.AuthoringJson,
                CompiledJson = version.CompiledJson,
                CreatedAt = version.CreatedAt,
                PublishedAt = version.PublishedAt
            });
        }
        else
        {
            if (string.Equals(row.Status, nameof(ProcessVersionStatus.Published), StringComparison.Ordinal)
                && row.AuthoringJson != version.AuthoringJson
                && version.Status == ProcessVersionStatus.Published)
            {
                throw new InvalidOperationException("Published process versions are immutable.");
            }

            row.DisplayName = version.DisplayName;
            row.Status = version.Status.ToString();
            row.AuthoringJson = version.AuthoringJson;
            row.CompiledJson = version.CompiledJson;
            row.PublishedAt = version.PublishedAt;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static ProcessDefinition MapDefinition(ProcessDefinitionRecord row) =>
        new()
        {
            DefinitionId = row.DefinitionId,
            EnvironmentId = row.EnvironmentId,
            ProjectId = row.ProjectId,
            Slug = row.Slug,
            DisplayName = row.DisplayName,
            CurrentPublishedVersionId = row.CurrentPublishedVersionId,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt
        };

    private static ProcessDefinitionVersion MapVersion(ProcessVersionRecord row) =>
        new()
        {
            VersionId = row.VersionId,
            DefinitionId = row.DefinitionId,
            EnvironmentId = row.EnvironmentId,
            Slug = row.Slug,
            DisplayName = row.DisplayName,
            VersionNumber = row.VersionNumber,
            Status = Enum.Parse<ProcessVersionStatus>(row.Status, ignoreCase: true),
            AuthoringJson = row.AuthoringJson,
            CompiledJson = row.CompiledJson,
            CreatedAt = row.CreatedAt,
            PublishedAt = row.PublishedAt
        };
}

public sealed class EfProcessInstanceStore : IProcessInstanceStore
{
    private readonly PlatformDbContext _db;

    public EfProcessInstanceStore(PlatformDbContext db) => _db = db;

    public async Task<ProcessInstance?> GetAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        var row = await _db.ProcessInstances.AsNoTracking()
            .FirstOrDefaultAsync(x => x.InstanceId == instanceId, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : Map(row);
    }

    public async Task<IReadOnlyList<ProcessInstance>> ListByDefinitionAsync(
        Guid definitionId,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.ProcessInstances.AsNoTracking()
            .Where(x => x.DefinitionId == definitionId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(Map).ToList();
    }

    public async Task SaveAsync(ProcessInstance instance, CancellationToken cancellationToken = default)
    {
        var row = await _db.ProcessInstances
            .FirstOrDefaultAsync(x => x.InstanceId == instance.InstanceId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            _db.ProcessInstances.Add(ToRecord(instance));
        }
        else
        {
            row.Status = instance.Status.ToString();
            row.VariablesJson = instance.VariablesJson;
            row.CursorJson = instance.CursorJson;
            row.FailureMessage = instance.FailureMessage;
            row.WaitingUntil = instance.WaitingUntil;
            row.UpdatedAt = instance.UpdatedAt;
            row.CompletedAt = instance.CompletedAt;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AppendLogAsync(ProcessActivityLogEntry entry, CancellationToken cancellationToken = default)
    {
        _db.ProcessActivityLogs.Add(new ProcessActivityLogRecord
        {
            LogId = entry.LogId,
            InstanceId = entry.InstanceId,
            StepId = entry.StepId,
            ActivityType = entry.ActivityType,
            Status = entry.Status,
            InputJson = entry.InputJson,
            OutputJson = entry.OutputJson,
            ErrorMessage = entry.ErrorMessage,
            StartedAt = entry.StartedAt,
            CompletedAt = entry.CompletedAt
        });
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProcessActivityLogEntry>> ListLogsAsync(
        Guid instanceId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.ProcessActivityLogs.AsNoTracking()
            .Where(x => x.InstanceId == instanceId)
            .OrderBy(x => x.StartedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(r => new ProcessActivityLogEntry
        {
            LogId = r.LogId,
            InstanceId = r.InstanceId,
            StepId = r.StepId,
            ActivityType = r.ActivityType,
            Status = r.Status,
            InputJson = r.InputJson,
            OutputJson = r.OutputJson,
            ErrorMessage = r.ErrorMessage,
            StartedAt = r.StartedAt,
            CompletedAt = r.CompletedAt
        }).ToList();
    }

    public async Task<IReadOnlyList<ProcessInstance>> ListWaitingDueAsync(
        DateTimeOffset asOf,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var status = nameof(ProcessInstanceStatus.Waiting);
        var rows = await _db.ProcessInstances.AsNoTracking()
            .Where(x => x.Status == status && x.WaitingUntil != null && x.WaitingUntil <= asOf)
            .OrderBy(x => x.WaitingUntil)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(Map).ToList();
    }

    private static ProcessInstanceRecord ToRecord(ProcessInstance instance) =>
        new()
        {
            InstanceId = instance.InstanceId,
            DefinitionId = instance.DefinitionId,
            VersionId = instance.VersionId,
            EnvironmentId = instance.EnvironmentId,
            ProjectId = instance.ProjectId,
            Slug = instance.Slug,
            Status = instance.Status.ToString(),
            VariablesJson = instance.VariablesJson,
            CursorJson = instance.CursorJson,
            FailureMessage = instance.FailureMessage,
            WaitingUntil = instance.WaitingUntil,
            PrincipalId = instance.PrincipalId,
            PrincipalRolesJson = instance.PrincipalRolesJson,
            CreatedAt = instance.CreatedAt,
            UpdatedAt = instance.UpdatedAt,
            CompletedAt = instance.CompletedAt
        };

    private static ProcessInstance Map(ProcessInstanceRecord row) =>
        new()
        {
            InstanceId = row.InstanceId,
            DefinitionId = row.DefinitionId,
            VersionId = row.VersionId,
            EnvironmentId = row.EnvironmentId,
            ProjectId = row.ProjectId,
            Slug = row.Slug,
            Status = Enum.Parse<ProcessInstanceStatus>(row.Status, ignoreCase: true),
            VariablesJson = row.VariablesJson,
            CursorJson = row.CursorJson,
            FailureMessage = row.FailureMessage,
            WaitingUntil = row.WaitingUntil,
            PrincipalId = row.PrincipalId,
            PrincipalRolesJson = row.PrincipalRolesJson,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt,
            CompletedAt = row.CompletedAt
        };
}
