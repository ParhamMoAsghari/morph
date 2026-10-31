# Concepts

## Organization → Project → Environment

| Concept | Meaning |
|---------|---------|
| **Organization** | Tenant boundary for people and billing-ish quotas |
| **Project** | App / product space; owns roles, policies, credentials, webhooks |
| **Environment** | Isolated runtime (e.g. `dev`, `staging`) — definitions, data, processes, functions |

Runtime data APIs require **`X-Morph-Env-Id`** (SDK sets this from `environmentId`).

## Users, tokens, credentials

- **Platform users** register with email/password; obtain JWT via OpenIddict password grant (`morph-platform` client in Development).
- **API credentials** (`morph_sk_…`) are project-scoped keys for automation (issue/list/revoke under `/v1/projects/{id}/credentials`).

## Definitions, versions, migrations

1. Create a **definition** (entity or component) with fields.
2. **Publish** a version (immutable published model).
3. **Plan / apply migration** so Postgres storage matches the effective model.
4. Apps write **records** against the entity slug.

Composition lets entities **use components**; decompose extracts fields into reusable components.

## Access

- Built-in roles typically include `owner`, `editor`, `viewer`.
- **Role bindings** attach a principal to a role (optionally per environment).
- **Policies** (PBAC) allow/deny actions on resources with conditions; publish before they apply.

## Processes & functions

- **Processes** — durable workflows authored as YAML/IR.
- **Functions** — registerable stubs (TypeScript/DotNet) invocable over HTTP or from processes. Full sandbox isolation is still evolving.

## Plugins

Folder under Host `plugins/` with `morph.plugin.json` + assembly implementing `IMorphPlugin`. Permissions are **default deny**. See [Plugin development](/plugins/).
