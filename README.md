# Morph

[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9-512BD4)](https://dotnet.microsoft.com/)
[![Docs](https://img.shields.io/badge/docs-VitePress-42b883)](apps/docs)

**Entity-driven Backend-as-a-Service** — define collections in YAML, enforce Access policies, serve records/files/GraphQL/processes from a modular .NET 9 host, and build apps with official SDKs and a Dev Panel.

> Open source under [Apache License 2.0](LICENSE). Collaboration policy: [CONTRIBUTING.md](CONTRIBUTING.md) · [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) · [GOVERNANCE.md](GOVERNANCE.md) · [SECURITY.md](SECURITY.md).

## Documentation

| | |
|--|--|
| **Online docs** | Deployed from [`apps/docs`](apps/docs) (VitePress) on Vercel — see the repo About / website link after deploy |
| **Local docs** | `cd apps/docs && npm install && npm run dev` (port **5180**) |
| **Validity gate** | [docs/test-based-functional-validity.md](docs/test-based-functional-validity.md) |

## Prerequisites

- .NET 9 SDK
- Node.js 18+ (dev panel / docs)
- Docker Desktop (Postgres 16, Redis 7, MinIO, Mailpit)

## Quick start

### 1. Start dependencies

```powershell
cd deploy
docker compose up -d postgres redis mailpit
```

Optional MinIO (needs Docker Hub pull access) and TLS edge:

```powershell
docker compose --profile storage up -d
docker compose --profile edge up -d caddy
```

Phase 0 Host boots without MinIO; leave `Morph:ObjectStorage:Endpoint` empty until storage is up.
### 2. Run the host

```powershell
cd ..
dotnet run --project src/Morph.Host --launch-profile http
```

On startup the host applies EF migrations and seeds the `morph-platform` OpenIddict client.

- API: http://localhost:5100  
- Health: http://localhost:5100/health/ready  
- **Dev panel:** http://localhost:5173 (see `apps/admin-console`)  
- Mailpit UI: http://localhost:8025  
- MinIO console: http://localhost:9001  

Configuration uses `appsettings.Development.json` and optional `MORPH_*` environment variables (see `.env.example`). Do not commit real secrets.

### 3. Register + obtain a JWT

```powershell
# Register
Invoke-RestMethod -Method Post -Uri http://localhost:5100/v1/auth/register `
  -ContentType "application/json" `
  -Body '{"email":"dev@morph.local","password":"Password1!","displayName":"Dev"}'

# Token (password grant — confidential morph-platform client; Phase 0 skeleton)
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

### 4. Build & test

```powershell
dotnet build Morph.sln
dotnet test Morph.sln
```

Host API e2e (Testcontainers Postgres — Docker required):

```powershell
dotnet test tests/Morph.Host.Integration.Tests
```

Coverage layers: unit (Access/EntityEngine/Process/Functions/Crypto/…), integration (Postgres/MinIO), architecture (NetArchTest), Host HTTP e2e for Platform + Runtime endpoints, .NET/TS SDK stubs.

**Functional validity (binding):** [docs/test-based-functional-validity.md](docs/test-based-functional-validity.md) — capability matrix, Dev Panel → API proof (Community Blog e2e), coverage snapshot, and release gates. Latest measured: **~289 .NET tests**, **~77.9% line / ~45.7% branch** (merged Cobertura).

## Solution layout

| Project | Role |
|---------|------|
| `Morph.BuildingBlocks` | Results, paging, clock |
| `Morph.Identity` | Platform users + auth contracts |
| `Morph.Tenancy` | Org / Project / Environment stubs |
| `Morph.Infrastructure` | EF Core, OpenIddict, DP keys, OTel/Serilog, health |
| `Morph.Platform.Api` | Auth + tenancy HTTP |
| `Morph.Runtime.Api` | Data API stubs |
| `Morph.Plugins.Abstractions` | Addon contracts (manifest, builder, providers) |
| `Morph.Plugins` | Plugin loader, ALC, permission gates |
| `Morph.Host` | Composition root |
| `addons/*` | Reference addons (Soap, Mellipayamak, Bale) |
| `tools/Morph.Cli` | `morph` CLI — schema sync, generate, plugins |
| `sdks/dotnet/Morph.Client` | Official .NET HTTP SDK |
| `sdks/typescript` | `@morph/sdk` TypeScript client |
| `templates/app-ts` | Code-first starter (no console) |

Code-first guide: [docs/code-first.md](docs/code-first.md) · Plugins: [Building an addon](docs/addons/building-an-addon.md) · [ADR-024](docs/adr/024-plugin-addon-system.md)

### CLI (Phase 5)

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

## Phase 0 acceptance checklist

- [x] `dotnet build` succeeds  
- [x] Compose deps + Host boot; `/health/ready` green when Postgres (and Redis/MinIO) are up  
- [x] Register + `/connect/token` returns JWT  
- [x] EF migrations apply for platform schema  
- [x] NetArchTest + CI workflow stub  

Entity engine, dynamic CRUD, Access, files, Hangfire, and SDKs start in Phase 1+.

## Contributing

Pull requests and issues are welcome. Please read **[CONTRIBUTING.md](CONTRIBUTING.md)** before opening a PR — that document is the collaboration policy (branching, review bar, licensing of contributions). Be kind: **[CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)**. Report vulnerabilities privately via **[SECURITY.md](SECURITY.md)**.

## License

Copyright 2026 Morph contributors. Licensed under the **Apache License, Version 2.0** — see [LICENSE](LICENSE) and [NOTICE](NOTICE).
