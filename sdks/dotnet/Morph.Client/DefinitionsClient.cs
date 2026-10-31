using Morph.Client.Internal;
using Morph.Client.Models;

namespace Morph.Client;

public sealed class DefinitionsClient
{
    private readonly MorphHttp _http;

    internal DefinitionsClient(MorphHttp http) => _http = http;

    public Task<List<DefinitionSummary>> ListAsync(
        Guid environmentId,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<List<DefinitionSummary>>(
            HttpMethod.Get,
            $"/v1/environments/{environmentId:D}/definitions",
            cancellationToken: cancellationToken);

    public Task<CreateDefinitionResponse> CreateAsync(
        Guid environmentId,
        CreateDefinitionRequest request,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<CreateDefinitionResponse>(
            HttpMethod.Post,
            $"/v1/environments/{environmentId:D}/definitions",
            request,
            cancellationToken: cancellationToken);

    public Task<CreateDefinitionResponse> ComposeAsync(
        Guid environmentId,
        ComposeDefinitionRequest request,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<CreateDefinitionResponse>(
            HttpMethod.Post,
            $"/v1/environments/{environmentId:D}/definitions/compose",
            request,
            cancellationToken: cancellationToken);

    public Task<DefinitionVersionDto> GetVersionAsync(
        Guid environmentId,
        Guid versionId,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<DefinitionVersionDto>(
            HttpMethod.Get,
            $"/v1/environments/{environmentId:D}/definitions/versions/{versionId:D}",
            cancellationToken: cancellationToken);

    public Task<DefinitionVersionDto> PublishAsync(
        Guid environmentId,
        Guid versionId,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<DefinitionVersionDto>(
            HttpMethod.Post,
            $"/v1/environments/{environmentId:D}/definitions/versions/{versionId:D}/publish",
            cancellationToken: cancellationToken);

    public Task<EffectiveModelDto> GetEffectiveModelAsync(
        Guid environmentId,
        Guid versionId,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<EffectiveModelDto>(
            HttpMethod.Get,
            $"/v1/environments/{environmentId:D}/definitions/versions/{versionId:D}/effective-model",
            cancellationToken: cancellationToken);

    public Task<object> ValidateAsync(
        Guid environmentId,
        Guid versionId,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<object>(
            HttpMethod.Post,
            $"/v1/environments/{environmentId:D}/definitions/versions/{versionId:D}/validate",
            cancellationToken: cancellationToken);
}
