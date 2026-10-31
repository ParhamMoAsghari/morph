# Builder APIs

`IPluginBuilder` is the **only** sanctioned registration surface.

```csharp
public interface IPluginBuilder
{
    string PluginId { get; }
    IReadOnlySet<string> GrantedPermissions { get; }

    void AddProcessActivity<TActivity>(string typeName) where TActivity : class, IProcessActivity;
    void AddSmsProvider<TProvider>() where TProvider : class, ISmsProvider;
    void AddInboundWebhook(string pathSuffix, IInboundWebhookHandler handler);
    void AddHttpRoute(string relativePath, Func<HttpContext, CancellationToken, Task> handler);
    void AddSecretSchema(JsonElement schema);
    IPluginHttpClientFactory GetHttpClientFactory();
    IMorphDataClient GetDataClient();
}
```

## Patterns

- Register everything inside `IMorphPlugin.RegisterAsync`.
- Activity `typeName` should be unique and namespaced (`plugin:acme.hello.doWork`).
- Prefer `GetHttpClientFactory()` over creating raw `HttpClient` (SSRF guards).
- Use `AddSecretSchema` so CLI / console can document required config keys.

See also: [activities](/plugins/activities), [SMS](/plugins/sms), [HTTP](/plugins/http), [outbound](/plugins/outbound).
