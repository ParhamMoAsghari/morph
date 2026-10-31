using System.Text.Json;
using Morph.Client.Internal;
using Morph.Client.Models;

namespace Morph.Client;

public sealed class DataClient
{
    private readonly MorphHttp _http;

    internal DataClient(MorphHttp http) => _http = http;

    public Task<PageDto<EntityRecordDto>> ListAsync(
        string entity,
        string? filter = null,
        string? sort = null,
        int? pageSize = null,
        string? cursor = null,
        Guid? environmentId = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(filter))
        {
            query.Add($"filter={Uri.EscapeDataString(filter)}");
        }

        if (!string.IsNullOrWhiteSpace(sort))
        {
            query.Add($"sort={Uri.EscapeDataString(sort)}");
        }

        if (pageSize is { } size)
        {
            query.Add($"pageSize={size}");
        }

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            query.Add($"cursor={Uri.EscapeDataString(cursor)}");
        }

        var qs = query.Count == 0 ? "" : "?" + string.Join("&", query);
        return _http.SendJsonAsync<PageDto<EntityRecordDto>>(
            HttpMethod.Get,
            $"/data/v1/{Uri.EscapeDataString(entity)}{qs}",
            environmentId: environmentId,
            includeEnv: true,
            cancellationToken: cancellationToken);
    }

    public Task<EntityRecordDto> GetAsync(
        string entity,
        Guid id,
        Guid? environmentId = null,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<EntityRecordDto>(
            HttpMethod.Get,
            $"/data/v1/{Uri.EscapeDataString(entity)}/{id:D}",
            environmentId: environmentId,
            includeEnv: true,
            cancellationToken: cancellationToken);

    public Task<EntityRecordDto> CreateAsync(
        string entity,
        object fields,
        Guid? environmentId = null,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<EntityRecordDto>(
            HttpMethod.Post,
            $"/data/v1/{Uri.EscapeDataString(entity)}",
            fields,
            environmentId,
            includeEnv: true,
            cancellationToken);

    public Task<EntityRecordDto> UpdateAsync(
        string entity,
        Guid id,
        object fields,
        Guid? environmentId = null,
        CancellationToken cancellationToken = default) =>
        _http.SendJsonAsync<EntityRecordDto>(
            HttpMethod.Patch,
            $"/data/v1/{Uri.EscapeDataString(entity)}/{id:D}",
            fields,
            environmentId,
            includeEnv: true,
            cancellationToken);

    public Task DeleteAsync(
        string entity,
        Guid id,
        Guid? environmentId = null,
        CancellationToken cancellationToken = default) =>
        _http.SendAsync(
            HttpMethod.Delete,
            $"/data/v1/{Uri.EscapeDataString(entity)}/{id:D}",
            environmentId: environmentId,
            includeEnv: true,
            cancellationToken: cancellationToken);

    public Task<JsonElement> StatusAsync(CancellationToken cancellationToken = default) =>
        _http.SendJsonElementAsync(HttpMethod.Get, "/data/v1/_meta/status", cancellationToken: cancellationToken);
}
