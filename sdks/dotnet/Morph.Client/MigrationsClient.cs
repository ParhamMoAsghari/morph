using Morph.Client.Internal;
using Morph.Client.Models;

namespace Morph.Client;

public sealed class MigrationsClient
{
    private readonly MorphHttp _http;

    internal MigrationsClient(MorphHttp http) => _http = http;

    public Task<MigrationPlanDto> PlanAsync(
        Guid environmentId,
        Guid toVersionId,
        Guid? fromVersionId = null,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<MigrationPlanDto>(
            HttpMethod.Post,
            $"/v1/environments/{environmentId:D}/migrations/plan",
            new { toVersionId, fromVersionId },
            cancellationToken: cancellationToken);

    public Task<MigrationRunDto> ApplyAsync(
        Guid environmentId,
        Guid toVersionId,
        Guid? fromVersionId = null,
        Guid? priorPublishedVersionId = null,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<MigrationRunDto>(
            HttpMethod.Post,
            $"/v1/environments/{environmentId:D}/migrations/apply",
            new { toVersionId, fromVersionId, priorPublishedVersionId },
            cancellationToken: cancellationToken);
}
