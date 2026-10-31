# Introduction

Morph is an **entity-driven Backend-as-a-Service**. You model your domain as versioned entity definitions, Morph materializes storage, and your apps talk to a secured data plane through REST, GraphQL, or governed SQL.

## Who it is for

- Teams that want **Supabase/PocketBase-style** speed with a .NET-owned stack
- Products that need **RBAC + attribute policies**, not only table grants
- Builders who prefer **git + CLI** but still want a **Dev Panel** for ops
- Integrators shipping **connectors** (SMS, bots, SOAP) as Morph plugins

## Product surfaces

| Surface | Purpose |
|---------|---------|
| **Morph.Host** | Composition root — all HTTP APIs |
| **Dev Panel** (`apps/admin-console`) | Browser UI over `@morph/sdk` |
| **CLI** (`tools/Morph.Cli`) | Schema push/publish/migrate, plugins, generate |
| **SDKs** | `@morph/sdk` (TS) and `Morph.Client` (.NET) |
| **Plugins** | Capability-based .NET addons under `/plugins` |

## Core ideas

1. **Definitions are versioned** — draft → publish → migrate storage.
2. **Environment scopes data** — send `X-Morph-Env-Id` (or SDK `environmentId`) for runtime APIs.
3. **Access is central** — bind a role (typically `owner` for yourself) before data CRUD succeeds.
4. **Plugins are sandboxed by permission** — default deny; SSRF-safe outbound HTTP.

## Prerequisites

- .NET 9 SDK
- Node.js 18+ (Dev Panel / docs)
- Docker Desktop (Postgres, Redis, Mailpit; optional MinIO)

Continue with [Quickstart](/guide/quickstart).
