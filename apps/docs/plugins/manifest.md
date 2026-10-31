# Manifest & permissions

## `morph.plugin.json`

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

| Field | Purpose |
|-------|---------|
| `id` | Stable plugin id; inbound routes live under `/plugins/v1/{id}/` |
| `version` | Semver-ish string for operators |
| `entryType` | Fully qualified `IMorphPlugin` type |
| `assembly` | DLL file name in the plugin folder |
| `permissions` | Granted capability set |
| `capabilities` | Advertised features (catalog / UX) |

The in-code `PluginManifest` on `IMorphPlugin` should stay aligned with the JSON file.

## Permission constants

Defined in `Morph.Plugins.Abstractions.PluginPermissions`:

| Constant | Value | Allows |
|----------|-------|--------|
| `ProcessActivities` | `process.activities` | `AddProcessActivity` |
| `SmsSend` | `sms.send` | `AddSmsProvider` |
| `HttpInbound` | `http.inbound` | `AddInboundWebhook`, `AddHttpRoute` |
| `HttpOutbound` | `http.outbound` | `GetHttpClientFactory` |
| `DataRead` | `data.read` | `GetDataClient` (read) |
| `DataWrite` | `data.write` | `GetDataClient` (write) |
| `SecretsRead` | `secrets.read` | Secret material access patterns |

Undeclared → **`PluginPermissionException`** at registration time.
