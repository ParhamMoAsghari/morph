using System.Text;
using System.Text.Json;
using Morph.Access.Domain;
using Morph.Access.Evaluation;
using Morph.BuildingBlocks.Security;
using Morph.DataPlane.Records;
using Morph.ProcessEngine.Execution;
using Morph.ProcessEngine.Ir;
using Morph.ProcessEngine.Ports;

namespace Morph.ProcessEngine.Activities;

public interface IBuiltinActivity
{
    string TypeName { get; }

    Task ExecuteAsync(CompiledStep step, ProcessExecutionContext context, CancellationToken cancellationToken);
}

public sealed class BuiltinActivityCatalog
{
    private readonly Dictionary<string, IBuiltinActivity> _map;

    public BuiltinActivityCatalog(IEnumerable<IBuiltinActivity> activities)
    {
        _map = activities.ToDictionary(a => a.TypeName, StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGet(string typeName, out IBuiltinActivity activity) =>
        _map.TryGetValue(typeName, out activity!);

    public IReadOnlyCollection<string> TypeNames => _map.Keys.ToArray();
}

public sealed class SetVariableActivity : IBuiltinActivity
{
    public string TypeName => ActivityTypes.SetVariable;

    public Task ExecuteAsync(CompiledStep step, ProcessExecutionContext context, CancellationToken cancellationToken)
    {
        var name = RequireString(step, context, "name");
        var valueEl = Require(step, "value");
        context.Variables.Set(name, context.Variables.Resolve(valueEl));
        return Task.CompletedTask;
    }

    internal static JsonElement Require(CompiledStep step, string key)
    {
        if (step.With is null || !step.With.TryGetValue(key, out var el))
        {
            throw new InvalidOperationException($"Activity '{step.Type}' requires '{key}'.");
        }

        return el;
    }

    internal static string RequireString(CompiledStep step, ProcessExecutionContext context, string key)
    {
        var el = Require(step, key);
        var resolved = context.Variables.Resolve(el);
        return resolved?.ToString()
            ?? throw new InvalidOperationException($"Activity '{step.Type}' '{key}' resolved to null.");
    }

    internal static string? OptionalString(CompiledStep step, ProcessExecutionContext context, string key)
    {
        if (step.With is null || !step.With.TryGetValue(key, out var el))
        {
            return null;
        }

        return context.Variables.Resolve(el)?.ToString();
    }
}

public sealed class StringConcatActivity : IBuiltinActivity
{
    public string TypeName => ActivityTypes.StringConcat;

    public Task ExecuteAsync(CompiledStep step, ProcessExecutionContext context, CancellationToken cancellationToken)
    {
        var partsEl = SetVariableActivity.Require(step, "parts");
        var parts = context.Variables.Resolve(partsEl) as IEnumerable<object?>
            ?? throw new InvalidOperationException("string.concat 'parts' must be an array.");
        var sep = SetVariableActivity.OptionalString(step, context, "separator") ?? "";
        var resultVar = SetVariableActivity.OptionalString(step, context, "resultVar") ?? "result";
        context.Variables.Set(resultVar, string.Join(sep, parts.Select(p => p?.ToString() ?? "")));
        return Task.CompletedTask;
    }
}

public sealed class StringReplaceActivity : IBuiltinActivity
{
    public string TypeName => ActivityTypes.StringReplace;

    public Task ExecuteAsync(CompiledStep step, ProcessExecutionContext context, CancellationToken cancellationToken)
    {
        var input = SetVariableActivity.RequireString(step, context, "input");
        var oldValue = SetVariableActivity.RequireString(step, context, "old");
        var newValue = SetVariableActivity.OptionalString(step, context, "new") ?? "";
        var resultVar = SetVariableActivity.OptionalString(step, context, "resultVar") ?? "result";
        context.Variables.Set(resultVar, input.Replace(oldValue, newValue, StringComparison.Ordinal));
        return Task.CompletedTask;
    }
}

public sealed class StringTrimActivity : IBuiltinActivity
{
    public string TypeName => ActivityTypes.StringTrim;

    public Task ExecuteAsync(CompiledStep step, ProcessExecutionContext context, CancellationToken cancellationToken)
    {
        var input = SetVariableActivity.RequireString(step, context, "input");
        var resultVar = SetVariableActivity.OptionalString(step, context, "resultVar") ?? "result";
        context.Variables.Set(resultVar, input.Trim());
        return Task.CompletedTask;
    }
}

public sealed class JsonParseActivity : IBuiltinActivity
{
    public string TypeName => ActivityTypes.JsonParse;

    public Task ExecuteAsync(CompiledStep step, ProcessExecutionContext context, CancellationToken cancellationToken)
    {
        var input = SetVariableActivity.RequireString(step, context, "input");
        var resultVar = SetVariableActivity.OptionalString(step, context, "resultVar") ?? "result";
        using var doc = JsonDocument.Parse(input);
        context.Variables.Set(resultVar, JsonElementToObject(doc.RootElement));
        return Task.CompletedTask;
    }

    internal static object? JsonElementToObject(JsonElement el) =>
        el.ValueKind switch
        {
            JsonValueKind.Object => el.EnumerateObject()
                .ToDictionary(p => p.Name, p => JsonElementToObject(p.Value), StringComparer.OrdinalIgnoreCase),
            JsonValueKind.Array => el.EnumerateArray().Select(JsonElementToObject).ToList(),
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
}

public sealed class JsonStringifyActivity : IBuiltinActivity
{
    public string TypeName => ActivityTypes.JsonStringify;

    public Task ExecuteAsync(CompiledStep step, ProcessExecutionContext context, CancellationToken cancellationToken)
    {
        var value = context.Variables.Resolve(SetVariableActivity.Require(step, "value"));
        var resultVar = SetVariableActivity.OptionalString(step, context, "resultVar") ?? "result";
        context.Variables.Set(resultVar, JsonSerializer.Serialize(value));
        return Task.CompletedTask;
    }
}

public sealed class JsonGetActivity : IBuiltinActivity
{
    public string TypeName => ActivityTypes.JsonGet;

    public Task ExecuteAsync(CompiledStep step, ProcessExecutionContext context, CancellationToken cancellationToken)
    {
        var path = SetVariableActivity.RequireString(step, context, "path");
        var resultVar = SetVariableActivity.OptionalString(step, context, "resultVar") ?? "result";
        context.Variables.Set(resultVar, context.Variables.GetPath(path));
        return Task.CompletedTask;
    }
}

public sealed class JsonSetActivity : IBuiltinActivity
{
    public string TypeName => ActivityTypes.JsonSet;

    public Task ExecuteAsync(CompiledStep step, ProcessExecutionContext context, CancellationToken cancellationToken)
    {
        var target = SetVariableActivity.RequireString(step, context, "target");
        var path = SetVariableActivity.OptionalString(step, context, "path");
        var value = context.Variables.Resolve(SetVariableActivity.Require(step, "value"));

        if (string.IsNullOrWhiteSpace(path))
        {
            context.Variables.Set(target, value);
            return Task.CompletedTask;
        }

        if (!context.Variables.TryGet(target, out var existing) || existing is not IDictionary<string, object?> dict)
        {
            dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            dict = new Dictionary<string, object?>(dict, StringComparer.OrdinalIgnoreCase);
        }

        dict[path] = value;
        context.Variables.Set(target, dict);
        return Task.CompletedTask;
    }
}

public sealed class JsonMergeActivity : IBuiltinActivity
{
    public string TypeName => ActivityTypes.JsonMerge;

    public Task ExecuteAsync(CompiledStep step, ProcessExecutionContext context, CancellationToken cancellationToken)
    {
        var target = SetVariableActivity.RequireString(step, context, "target");
        var source = context.Variables.Resolve(SetVariableActivity.Require(step, "source"))
            as IDictionary<string, object?>
            ?? throw new InvalidOperationException("json.merge 'source' must be an object.");

        if (!context.Variables.TryGet(target, out var existing) || existing is not IDictionary<string, object?> dict)
        {
            dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            dict = new Dictionary<string, object?>(dict, StringComparer.OrdinalIgnoreCase);
        }

        foreach (var (k, v) in source)
        {
            dict[k] = v;
        }

        context.Variables.Set(target, dict);
        return Task.CompletedTask;
    }
}

public sealed class HttpRequestActivity : IBuiltinActivity
{
    public string TypeName => ActivityTypes.HttpRequest;

    public async Task ExecuteAsync(CompiledStep step, ProcessExecutionContext context, CancellationToken cancellationToken)
    {
        var method = SetVariableActivity.OptionalString(step, context, "method") ?? "GET";
        var url = SetVariableActivity.RequireString(step, context, "url");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"http.request url is not absolute: {url}");
        }

        await SsrfGuard.EnsurePublicEndpointAsync(uri, cancellationToken).ConfigureAwait(false);

        using var request = new HttpRequestMessage(new HttpMethod(method), uri);
        if (step.With is not null && step.With.TryGetValue("headers", out var headersEl))
        {
            var headers = context.Variables.Resolve(headersEl) as IDictionary<string, object?>;
            if (headers is not null)
            {
                foreach (var (k, v) in headers)
                {
                    request.Headers.TryAddWithoutValidation(k, v?.ToString());
                }
            }
        }

        if (step.With is not null && step.With.TryGetValue("body", out var bodyEl)
            && !string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase))
        {
            var body = context.Variables.Resolve(bodyEl);
            var json = body is string s ? s : JsonSerializer.Serialize(body);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        var client = context.Http.CreateClient();
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        object? parsedBody = content;
        try
        {
            using var doc = JsonDocument.Parse(content);
            parsedBody = JsonParseActivity.JsonElementToObject(doc.RootElement);
        }
        catch (JsonException)
        {
            // keep raw string
        }

        var resultVar = SetVariableActivity.OptionalString(step, context, "resultVar") ?? "http";
        context.Variables.Set(resultVar, new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["statusCode"] = (int)response.StatusCode,
            ["body"] = parsedBody,
            ["headers"] = response.Headers
                .Concat(response.Content.Headers)
                .GroupBy(h => h.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => (object?)string.Join(",", g.SelectMany(x => x.Value)), StringComparer.OrdinalIgnoreCase)
        });
    }
}

public sealed class MorphDataCreateActivity : IBuiltinActivity
{
    public string TypeName => ActivityTypes.MorphDataCreate;

    public async Task ExecuteAsync(CompiledStep step, ProcessExecutionContext context, CancellationToken cancellationToken)
    {
        var entity = SetVariableActivity.RequireString(step, context, "entity");
        var fieldsObj = MorphDataCreateActivity.ToReadOnlyFields(
            context.Variables.Resolve(SetVariableActivity.Require(step, "fields")),
            "morph.data.create");

        var result = await context.DataPlane.CreateAsync(
                context.Instance.EnvironmentId,
                entity,
                fieldsObj,
                context.Subject,
                cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error!.Message);
        }

        var resultVar = SetVariableActivity.OptionalString(step, context, "resultVar") ?? "record";
        context.Variables.Set(resultVar, RecordToDict(result.Value));
    }

    internal static Dictionary<string, object?> RecordToDict(EntityRecordDocument record)
    {
        var dict = new Dictionary<string, object?>(record.Fields, StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = record.Id,
            ["createdAt"] = record.CreatedAt,
            ["updatedAt"] = record.UpdatedAt
        };
        return dict;
    }

    internal static IReadOnlyDictionary<string, object?> ToReadOnlyFields(object? value, string activity)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnly)
        {
            return readOnly;
        }

        if (value is IDictionary<string, object?> mutable)
        {
            return new Dictionary<string, object?>(mutable, StringComparer.OrdinalIgnoreCase);
        }

        if (value is Dictionary<string, JsonElement> jsonMap)
        {
            return jsonMap.ToDictionary(
                kv => kv.Key,
                kv => (object?)JsonElementToObject(kv.Value),
                StringComparer.OrdinalIgnoreCase);
        }

        throw new InvalidOperationException($"{activity} 'fields' must be an object.");
    }

    private static object? JsonElementToObject(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Array => el.EnumerateArray().Select(JsonElementToObject).ToList(),
        JsonValueKind.Object => el.EnumerateObject()
            .ToDictionary(p => p.Name, p => JsonElementToObject(p.Value), StringComparer.OrdinalIgnoreCase),
        _ => el.ToString()
    };
}

public sealed class MorphDataGetActivity : IBuiltinActivity
{
    public string TypeName => ActivityTypes.MorphDataGet;

    public async Task ExecuteAsync(CompiledStep step, ProcessExecutionContext context, CancellationToken cancellationToken)
    {
        var entity = SetVariableActivity.RequireString(step, context, "entity");
        var idRaw = SetVariableActivity.RequireString(step, context, "id");
        if (!Guid.TryParse(idRaw, out var id))
        {
            throw new InvalidOperationException("morph.data.get 'id' must be a GUID.");
        }

        var result = await context.DataPlane.GetAsync(
                context.Instance.EnvironmentId,
                entity,
                id,
                context.Subject,
                cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error!.Message);
        }

        var resultVar = SetVariableActivity.OptionalString(step, context, "resultVar") ?? "record";
        context.Variables.Set(resultVar, MorphDataCreateActivity.RecordToDict(result.Value));
    }
}

public sealed class MorphDataUpdateActivity : IBuiltinActivity
{
    public string TypeName => ActivityTypes.MorphDataUpdate;

    public async Task ExecuteAsync(CompiledStep step, ProcessExecutionContext context, CancellationToken cancellationToken)
    {
        var entity = SetVariableActivity.RequireString(step, context, "entity");
        var idRaw = SetVariableActivity.RequireString(step, context, "id");
        if (!Guid.TryParse(idRaw, out var id))
        {
            throw new InvalidOperationException("morph.data.update 'id' must be a GUID.");
        }

        var fieldsObj = MorphDataCreateActivity.ToReadOnlyFields(
            context.Variables.Resolve(SetVariableActivity.Require(step, "fields")),
            "morph.data.update");

        var result = await context.DataPlane.UpdateAsync(
                context.Instance.EnvironmentId,
                entity,
                id,
                fieldsObj,
                context.Subject,
                cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error!.Message);
        }

        var resultVar = SetVariableActivity.OptionalString(step, context, "resultVar") ?? "record";
        context.Variables.Set(resultVar, MorphDataCreateActivity.RecordToDict(result.Value));
    }
}

public sealed class MorphDataDeleteActivity : IBuiltinActivity
{
    public string TypeName => ActivityTypes.MorphDataDelete;

    public async Task ExecuteAsync(CompiledStep step, ProcessExecutionContext context, CancellationToken cancellationToken)
    {
        var entity = SetVariableActivity.RequireString(step, context, "entity");
        var idRaw = SetVariableActivity.RequireString(step, context, "id");
        if (!Guid.TryParse(idRaw, out var id))
        {
            throw new InvalidOperationException("morph.data.delete 'id' must be a GUID.");
        }

        var result = await context.DataPlane.DeleteAsync(
                context.Instance.EnvironmentId,
                entity,
                id,
                context.Subject,
                cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error!.Message);
        }
    }
}

public sealed class MorphDataListActivity : IBuiltinActivity
{
    public string TypeName => ActivityTypes.MorphDataList;

    public async Task ExecuteAsync(CompiledStep step, ProcessExecutionContext context, CancellationToken cancellationToken)
    {
        var entity = SetVariableActivity.RequireString(step, context, "entity");
        var pageSize = 50;
        if (step.With is not null && step.With.TryGetValue("pageSize", out var ps)
            && context.Variables.Resolve(ps) is IConvertible c)
        {
            pageSize = Convert.ToInt32(c);
        }

        var result = await context.DataPlane.ListAsync(
                context.Instance.EnvironmentId,
                entity,
                new DynamicListRequest { PageSize = pageSize },
                context.Subject,
                cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error!.Message);
        }

        var resultVar = SetVariableActivity.OptionalString(step, context, "resultVar") ?? "records";
        context.Variables.Set(
            resultVar,
            result.Value.Items.Select(MorphDataCreateActivity.RecordToDict).ToList());
    }
}

public sealed class DelayActivity : IBuiltinActivity
{
    public string TypeName => ActivityTypes.Delay;

    public Task ExecuteAsync(CompiledStep step, ProcessExecutionContext context, CancellationToken cancellationToken)
    {
        // Handled specially by ProcessRunner (durable wait).
        return Task.CompletedTask;
    }

    public static TimeSpan ResolveDelay(CompiledStep step, ProcessExecutionContext context)
    {
        if (step.With is not null && step.With.TryGetValue("seconds", out var secEl))
        {
            var sec = Convert.ToDouble(context.Variables.Resolve(secEl) ?? 0);
            return TimeSpan.FromSeconds(sec);
        }

        if (step.With is not null && step.With.TryGetValue("milliseconds", out var msEl))
        {
            var ms = Convert.ToDouble(context.Variables.Resolve(msEl) ?? 0);
            return TimeSpan.FromMilliseconds(ms);
        }

        throw new InvalidOperationException("delay requires 'seconds' or 'milliseconds'.");
    }
}

public sealed class AccessAssertActivity : IBuiltinActivity
{
    public string TypeName => ActivityTypes.AccessAssert;

    public async Task ExecuteAsync(CompiledStep step, ProcessExecutionContext context, CancellationToken cancellationToken)
    {
        var action = SetVariableActivity.RequireString(step, context, "action");
        var resourceType = SetVariableActivity.RequireString(step, context, "resource");
        var access = context.GetRequiredService<IAccessEvaluator>();

        var decision = await access.EvaluateAsync(
                new AccessContext
                {
                    Subject = context.Subject,
                    Action = action,
                    Resource = new AccessResource
                    {
                        Type = resourceType,
                        ProjectId = context.Instance.ProjectId,
                        EnvironmentId = context.Instance.EnvironmentId
                    }
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (decision.IsDenied)
        {
            throw new InvalidOperationException($"access.assert denied: {decision.Reason}");
        }
    }
}

public sealed class InvokeFunctionActivity : IBuiltinActivity
{
    public string TypeName => ActivityTypes.InvokeFunction;

    public async Task ExecuteAsync(CompiledStep step, ProcessExecutionContext context, CancellationToken cancellationToken)
    {
        var slug = SetVariableActivity.OptionalString(step, context, "slug")
            ?? SetVariableActivity.OptionalString(step, context, "function")
            ?? throw new InvalidOperationException(
                "morph.functions.invoke requires 'slug' or 'function'.");

        object? input = null;
        if (step.With is not null && step.With.TryGetValue("input", out var inputEl))
        {
            input = context.Variables.Resolve(inputEl);
        }

        var invoker = context.GetRequiredService<IProcessFunctionInvoker>();
        var output = await invoker
            .InvokeAsync(context.Instance.EnvironmentId, slug, input, cancellationToken)
            .ConfigureAwait(false);

        var resultVar = SetVariableActivity.OptionalString(step, context, "resultVar") ?? "functionResult";
        context.Variables.Set(resultVar, output);
    }
}
