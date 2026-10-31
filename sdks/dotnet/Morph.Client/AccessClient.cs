using Morph.Client.Internal;
using Morph.Client.Models;

namespace Morph.Client;

public sealed class AccessClient
{
    private readonly MorphHttp _http;

    internal AccessClient(MorphHttp http) => _http = http;

    public Task<List<RoleDto>> ListRolesAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<List<RoleDto>>(
            HttpMethod.Get,
            $"/v1/projects/{projectId:D}/roles",
            cancellationToken: cancellationToken);

    public Task<RoleDto> CreateRoleAsync(
        Guid projectId,
        CreateRoleRequest request,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<RoleDto>(
            HttpMethod.Post,
            $"/v1/projects/{projectId:D}/roles",
            request,
            cancellationToken: cancellationToken);

    public Task<object> BindRoleAsync(
        Guid projectId,
        BindRoleRequest request,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<object>(
            HttpMethod.Post,
            $"/v1/projects/{projectId:D}/role-bindings",
            request,
            cancellationToken: cancellationToken);

    public Task<List<PolicyDto>> ListPoliciesAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<List<PolicyDto>>(
            HttpMethod.Get,
            $"/v1/projects/{projectId:D}/policies",
            cancellationToken: cancellationToken);

    public Task<PolicyDto> CreatePolicyAsync(
        Guid projectId,
        CreatePolicyRequest request,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<PolicyDto>(
            HttpMethod.Post,
            $"/v1/projects/{projectId:D}/policies",
            request,
            cancellationToken: cancellationToken);

    public Task<PolicyDto> PublishPolicyAsync(
        Guid projectId,
        Guid policyId,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<PolicyDto>(
            HttpMethod.Post,
            $"/v1/projects/{projectId:D}/policies/{policyId:D}/publish",
            cancellationToken: cancellationToken);

    /// <summary>
    /// Dry-run Access decision. Platform API does not expose simulate yet — callers should catch MorphApiException 404.
    /// </summary>
    public Task<object> SimulateAsync(
        object request,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<object>(
            HttpMethod.Post,
            "/v1/access/simulate",
            request,
            cancellationToken: cancellationToken);
}
