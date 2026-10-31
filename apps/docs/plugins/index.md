# Plugin development

Morph plugins (addons) extend the Host **without forking it**. They ship as a folder:

- `morph.plugin.json` — id, version, permissions, capabilities, entry type
- A .NET assembly implementing **`IMorphPlugin`**

Reference **only** `Morph.Plugins.Abstractions`. Do not take dependencies on Host, Infrastructure, or EF.

## What you can build

| Capability | Builder API | Permission |
|------------|-------------|------------|
| Process activities | `AddProcessActivity<T>` | `process.activities` |
| SMS providers | `AddSmsProvider<T>` | `sms.send` |
| Inbound webhooks / HTTP | `AddInboundWebhook` / `AddHttpRoute` | `http.inbound` |
| Outbound HTTP | `GetHttpClientFactory()` | `http.outbound` |
| Data client | `GetDataClient()` | `data.read` / `data.write` |
| Secret schema | `AddSecretSchema` | (declared for ops UX) |

Permissions are **default deny**. Calling a builder method without declaring the permission throws `PluginPermissionException`.

## Start here

1. [Quickstart addon](/plugins/quickstart)
2. [Manifest & permissions](/plugins/manifest)
3. [Builder APIs](/plugins/builder)
4. [Install & load](/plugins/install)
5. [Reference addons](/plugins/reference-addons) (Soap, Mellipayamak, Bale)
6. [Security model](/plugins/security)

Architectural decision: [ADR-024](/reference/adrs).
