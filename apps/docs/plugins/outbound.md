# Outbound HTTP (SSRF-safe)

```csharp
var factory = builder.GetHttpClientFactory();
```

Requires `http.outbound`.

The Host injects an `IPluginHttpClientFactory` that blocks private/link-local targets and other SSRF footguns. Do **not** new up unrestricted `HttpClient` for plugin egress — it is outside the sanctioned surface and may be blocked or removed.

Webhook **subscriptions** from Platform APIs similarly reject private `targetUrl` values.
