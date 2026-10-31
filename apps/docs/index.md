---
layout: home
title: Morph Docs
hero:
  name: Morph
  text: Entity-driven Backend-as-a-Service
  tagline: Auth, collections, access policies, files, GraphQL, governed SQL, processes, functions, and plugins — one .NET host you can run locally in minutes.
  actions:
    - theme: brand
      text: Start now
      link: /guide/quickstart
    - theme: alt
      text: Build a plugin
      link: /plugins/
    - theme: alt
      text: API reference
      link: /api/
features:
  - title: Collections & data API
    details: Define entities, publish versions, migrate storage, then CRUD over REST — with GraphQL and governed SQL as first-class query surfaces.
  - title: Unified access
    details: RBAC + ABAC + PBAC in Morph.Access. Roles, bindings, and policies gate the data plane the same way for panel, SDK, and plugins.
  - title: Automation
    details: YAML process engine with durable runs, stub Functions, signed webhooks, Hangfire jobs, and SMS/email facades.
  - title: Extensible plugins
    details: Capability-based .NET addons with default-deny permissions, SSRF-safe outbound HTTP, and Process/SMS/inbound hooks — no Host fork required.
  - title: Code-first or panel
    details: Ship with morph CLI + SDKs, or use the Dev Panel (PocketBase-style) that speaks only @morph/sdk.
  - title: Proven by tests
    details: Host e2e, Testcontainers, and a Community Blog workflow that walks every panel API. See Testing & validity for the gate.
---

## Start in three steps

::: code-group

```powershell [PowerShell]
cd deploy
docker compose up -d postgres redis mailpit

cd ..
dotnet run --project src/Morph.Host --launch-profile http
# API → http://localhost:5100
```

```bash [Bash]
cd deploy
docker compose up -d postgres redis mailpit

cd ..
dotnet run --project src/Morph.Host --launch-profile http
# API → http://localhost:5100
```

:::

Then open the **Dev Panel**:

```powershell
cd apps/admin-console
npm install
npm run dev
# → http://127.0.0.1:5173
```

Or jump into the [full Quickstart](/guide/quickstart) for register → JWT → org/project/env → first collection → first record.

## What Morph is

Morph is a **modular monolith** BaaS: one Host process composes Identity (OpenIddict), tenancy, entity engine, access, data plane, files, messaging, processes, functions, SMS/mail, and a plugin loader. You own the data plane; Morph owns schema evolution, authorization, and the operator surfaces.

| You want to… | Go here |
|--------------|---------|
| Run locally today | [Quickstart](/guide/quickstart) |
| Click around like PocketBase/Supabase | [Dev Panel](/guide/dev-panel) |
| Schema-as-code in git | [CLI & code-first](/guide/cli) |
| Call Morph from an app | [TypeScript SDK](/sdk/typescript) · [.NET SDK](/sdk/dotnet) |
| Extend Morph | [Plugin development](/plugins/) |
| Understand internals | [Architecture](/architecture/) |
| Verify “it works” | [Testing & validity](/reference/testing) |

## Local endpoints

| Surface | URL |
|---------|-----|
| Host API | http://localhost:5100 |
| Health | http://localhost:5100/health/ready |
| GraphQL | http://localhost:5100/graphql |
| OpenAPI (Development) | http://localhost:5100/openapi/v1.json |
| Dev Panel | http://127.0.0.1:5173 |
| Mailpit | http://localhost:8025 |
| Hangfire (dev) | http://localhost:5100/internal/hangfire |
