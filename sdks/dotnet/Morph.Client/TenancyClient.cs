using Morph.Client.Internal;
using Morph.Client.Models;

namespace Morph.Client;

public sealed class TenancyClient
{
    private readonly MorphHttp _http;

    internal TenancyClient(MorphHttp http) => _http = http;

    public Task<OrganizationDto> CreateOrganizationAsync(
        CreateOrganizationRequest request,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<OrganizationDto>(HttpMethod.Post, "/v1/orgs", request, cancellationToken: cancellationToken);

    public Task<List<OrganizationDto>> ListOrganizationsAsync(CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<List<OrganizationDto>>(HttpMethod.Get, "/v1/orgs", cancellationToken: cancellationToken);

    public Task<ProjectDto> CreateProjectAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<ProjectDto>(HttpMethod.Post, "/v1/projects", request, cancellationToken: cancellationToken);

    public Task<List<ProjectDto>> ListProjectsAsync(CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<List<ProjectDto>>(HttpMethod.Get, "/v1/projects", cancellationToken: cancellationToken);

    public Task<EnvironmentDto> CreateEnvironmentAsync(
        Guid projectId,
        CreateEnvironmentRequest request,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<EnvironmentDto>(
            HttpMethod.Post,
            $"/v1/projects/{projectId:D}/environments",
            request,
            cancellationToken: cancellationToken);

    public Task<List<EnvironmentDto>> ListEnvironmentsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<List<EnvironmentDto>>(
            HttpMethod.Get,
            $"/v1/projects/{projectId:D}/environments",
            cancellationToken: cancellationToken);
}
