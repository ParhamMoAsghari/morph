# Building a Morph addon

> **Full plugin docs (recommended):** run the VitePress site in [`apps/docs`](../../apps/docs) and open **Plugins** — quickstart, manifest, builder APIs, security, and reference addons.

Addons extend Morph without forking the Host. They ship as a folder containing:

- `morph.plugin.json` — identity, permissions, capabilities, config schema
- A .NET assembly that implements `IMorphPlugin`

## Contract

Reference only **`Morph.Plugins.Abstractions`**. Do not reference Host, Infrastructure, or EF.

```csharp
public sealed class MyPlugin : IMorphPlugin
{
    public PluginManifest Manifest { get; } = new()
    {
        Id = "morph.example",
        Version = "1.0.0",
        DisplayName = "Example",
        Permissions = [PluginPermissions.ProcessActivities, PluginPermissions.HttpOutbound]
    };

    public Task RegisterAsync(IPluginBuilder builder, CancellationToken ct)
    {
        builder.AddProcessActivity<MyActivity>("plugin:morph.example.doWork");
        return Task.CompletedTask;
    }
}
```

## Permissions (default deny)

| Permission | Allows |
|------------|--------|
| `process.activities` | `AddProcessActivity` |
| `sms.send` | `AddSmsProvider` |
| `http.inbound` | `AddInboundWebhook` / `AddHttpRoute` |
| `http.outbound` | `GetHttpClientFactory` (SSRF-safe) |
| `data.read` / `data.write` | `GetDataClient` (Access-enforced later) |
| `secrets.read` | `AddSecretSchema` |

Calling a builder API without declaring the permission throws `PluginPermissionException`.

## Manifest

```json
{
  "id": "morph.example",
  "version": "1.0.0",
  "displayName": "Example",
  "entryType": "MyCompany.MyPlugin",
  "assembly": "MyCompany.MyPlugin.dll",
  "permissions": ["process.activities", "http.outbound"],
  "capabilities": ["process.activity"]
}
```

## Install & run

```powershell
# Build a reference addon
dotnet build addons/Morph.Addon.Soap

# Install into Host plugins directory
dotnet run --project tools/Morph.Cli -- plugin install addons/Morph.Addon.Soap/bin/Debug/net9.0 --path src/Morph.Host/plugins

# Or set MORPH_Plugins__Path and start Host
$env:MORPH_Plugins__Path = "src/Morph.Host/plugins"
dotnet run --project src/Morph.Host --launch-profile http
```

List loaded plugins: `GET /plugins/v1`  
Bale webhook example: `POST /plugins/v1/morph.bale/updates`

## Code-first projects

```yaml
# morph.yaml
plugins:
  - id: morph.soap
    version: "1.0.0"
  - id: morph.mellipayamak
    version: "1.0.0"
    configRef: secrets/mellipayamak
  - id: morph.bale
    version: "1.0.0"
    configRef: secrets/bale
```

## Reference addons

| Addon | Id | Notes |
|-------|-----|-------|
| `Morph.Addon.Soap` | `morph.soap` | `plugin:morph.soap.invoke` |
| `Morph.Addon.Mellipayamak` | `morph.mellipayamak` | `ISmsProvider` + send activity stub |
| `Morph.Addon.Bale` | `morph.bale` | inbound webhook + send/callback stubs |

See [ADR-024](../adr/024-plugin-addon-system.md) for the architectural decision.
