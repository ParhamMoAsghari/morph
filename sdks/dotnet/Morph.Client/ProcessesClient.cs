using Morph.Client.Internal;
using Morph.Client.Models;

namespace Morph.Client;

public sealed class ProcessesClient
{
    private readonly MorphHttp _http;

    internal ProcessesClient(MorphHttp http) => _http = http;

    public Task<IReadOnlyList<ProcessDefinitionDto>> ListAsync(
        Guid environmentId,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<IReadOnlyList<ProcessDefinitionDto>>(
            HttpMethod.Get,
            $"/v1/environments/{environmentId:D}/processes",
            cancellationToken: cancellationToken);

    public Task<ProcessUpsertResponse> PushAsync(
        Guid environmentId,
        ProcessUpsertRequest request,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<ProcessUpsertResponse>(
            HttpMethod.Post,
            $"/v1/environments/{environmentId:D}/processes/push",
            request,
            cancellationToken: cancellationToken);

    public Task<ProcessVersionDto> PublishAsync(
        Guid environmentId,
        Guid versionId,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<ProcessVersionDto>(
            HttpMethod.Post,
            $"/v1/environments/{environmentId:D}/processes/versions/{versionId:D}/publish",
            cancellationToken: cancellationToken);

    public Task<ProcessAuthoringDto> GetAuthoringAsync(
        Guid environmentId,
        Guid versionId,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<ProcessAuthoringDto>(
            HttpMethod.Get,
            $"/v1/environments/{environmentId:D}/processes/versions/{versionId:D}/authoring",
            cancellationToken: cancellationToken);

    public Task<ProcessInstanceDto> RunAsync(
        Guid environmentId,
        string slug,
        ProcessRunRequest? request = null,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<ProcessInstanceDto>(
            HttpMethod.Post,
            $"/v1/environments/{environmentId:D}/processes/{Uri.EscapeDataString(slug)}/run",
            request ?? new ProcessRunRequest(),
            cancellationToken: cancellationToken);

    public Task<IReadOnlyList<ProcessActivityLogDto>> LogsAsync(
        Guid environmentId,
        Guid instanceId,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<IReadOnlyList<ProcessActivityLogDto>>(
            HttpMethod.Get,
            $"/v1/environments/{environmentId:D}/processes/instances/{instanceId:D}/logs",
            cancellationToken: cancellationToken);
}
