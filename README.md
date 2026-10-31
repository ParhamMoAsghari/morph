# Morph

[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)
[![CI](https://github.com/ParhamMoAsghari/morph/actions/workflows/ci.yml/badge.svg)](https://github.com/ParhamMoAsghari/morph/actions/workflows/ci.yml)
[![Docs](https://img.shields.io/badge/docs-live-42b883)](https://morph-baas.vercel.app)
[![.NET](https://img.shields.io/badge/.NET-9-512BD4)](https://dotnet.microsoft.com/)

**Entity-driven Backend-as-a-Service** — define collections in YAML (or the Dev Panel), enforce Access policies (RBAC + ABAC + PBAC), and serve records, files, GraphQL, governed SQL, processes, and functions from a modular **.NET 9** host.

| | |
|--|--|
| **Docs** | https://morph-baas.vercel.app |
| **License** | [Apache-2.0](LICENSE) |
| **Collaborate** | [CONTRIBUTING](CONTRIBUTING.md) · [Code of Conduct](CODE_OF_CONDUCT.md) · [Governance](GOVERNANCE.md) · [Security](SECURITY.md) |

## Why Morph?

- **Schema as code** — entity definitions, policies, and processes in git; `morph` CLI push / publish / migrate
- **One Host** — Identity (OpenIddict), tenancy, Access, DataPlane, files, processes, functions, plugins
- **Dev Panel** — PocketBase-style browser UI that speaks only `@morph/sdk`
- **Extensible** — capability-based .NET plugins (SMS, bots, SOAP, …) without forking the Host
- **Proven by tests** — Host e2e + Community Blog panel workflow; see [functional validity](docs/test-based-functional-validity.md)

## Quick start

### Prerequisites

- .NET 9 SDK
- Node.js 18+ (Dev Panel / local docs)
- Docker Desktop (Postgres 16, Redis 7, Mailpit; MinIO optional)

### 1. Dependencies + Host

```powershell
cd deploy
docker compose up -d postgres redis mailpit

cd ..
dotnet run --project src/Morph.Host --launch-profile http
```

- API: http://localhost:5100  
- Health: http://localhost:5100/health/ready  
- Mailpit: http://localhost:8025  

Optional storage: `docker compose --profile storage up -d` (MinIO console :9001).

Config: `appsettings.Development.json` and `MORPH_*` env vars — see [`.env.example`](.env.example). **Do not commit secrets.**

### 2. Register and get a JWT

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:5100/v1/auth/register `
  -ContentType "application/json" `
  -Body '{"email":"dev@morph.local","password":"Password1!","displayName":"Dev"}'

$body = @{
  grant_type    = "password"
  username      = "dev@morph.local"
  password      = "Password1!"
  client_id     = "morph-platform"
  client_secret = "morph-platform-dev-secret"
  scope         = "openid profile email offline_access morph.platform"
}
Invoke-RestMethod -Method Post -Uri http://localhost:5100/connect/token -Body $body
```

Then create an org → project → environment and bind yourself as `owner` (required for data CRUD). Full walkthrough: [Quickstart](https://morph-baas.vercel.app/guide/quickstart).

### 3. Dev Panel (optional)

```powershell
cd apps/admin-console
npm install
npm run dev
# → http://127.0.0.1:5173
```

### 4. Build & test

```powershell
dotnet build Morph.sln
dotnet test Morph.sln
```

Host API e2e (Docker / Testcontainers):

```powershell
dotnet test tests/Morph.Host.Integration.Tests
```

## Documentation

| Resource | Link |
|----------|------|
| Online docs | https://morph-baas.vercel.app |
| Local VitePress | `cd apps/docs && npm install && npm run dev` (port **5180**) |
| Functional validity | [docs/test-based-functional-validity.md](docs/test-based-functional-validity.md) |
| Code-first | [docs/code-first.md](docs/code-first.md) |
| Plugin addons | [docs/addons/building-an-addon.md](docs/addons/building-an-addon.md) |
| ADRs | [docs/adr/](docs/adr/) |

## CLI (schema as code)

```powershell
dotnet run --project tools/Morph.Cli -- init
dotnet run --project tools/Morph.Cli -- login --email dev@morph.local --password 'Password1!'
dotnet run --project tools/Morph.Cli -- link --project <guid> --env <guid>
dotnet run --project tools/Morph.Cli -- push
dotnet run --project tools/Morph.Cli -- publish
dotnet run --project tools/Morph.Cli -- migrate --apply
dotnet run --project tools/Morph.Cli -- generate
dotnet run --project tools/Morph.Cli -- plugin list
```

## Repository layout

| Path | Role |
|------|------|
| `src/Morph.Host` | Composition root (HTTP API) |
| `src/Morph.*` | Engines & libraries (Access, EntityEngine, DataPlane, Process, Plugins, …) |
| `addons/*` | Reference plugins (Soap, Mellipayamak, Bale) |
| `tools/Morph.Cli` | `morph` CLI |
| `sdks/typescript` | `@morph/sdk` |
| `sdks/dotnet/Morph.Client` | .NET SDK |
| `apps/admin-console` | Dev Panel |
| `apps/docs` | VitePress docs (deployed to Vercel) |
| `deploy/` | Docker Compose |
| `tests/` | Unit, architecture, integration, Host e2e |

## Contributing

PRs and issues are welcome.

1. Read **[CONTRIBUTING.md](CONTRIBUTING.md)** (branching, review bar, contribution license).
2. Follow the **[Code of Conduct](CODE_OF_CONDUCT.md)**.
3. Report vulnerabilities privately via **[SECURITY.md](SECURITY.md)** — not public issues.

## License

Copyright 2026 Morph contributors.

Licensed under the **Apache License, Version 2.0** — see [LICENSE](LICENSE) and [NOTICE](NOTICE).
