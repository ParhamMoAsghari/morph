# Test-based functional validity of Morph

This document is the product gate for **“Morph works as claimed.”**  
It binds every major capability to executable tests with **ideal contract assertions** (status codes, response shapes, side effects)—not DTO construction or “status is one of several codes.”

Product documentation site: [`apps/docs`](../apps/docs) (VitePress — Start now, guides, API, plugins). Summary page: `/reference/testing`.

Measured on **2026-10-29** against `Morph.sln` with Docker/Testcontainers available.

---

## 1. Snapshot (re-measure after material changes)

| Metric | Value |
|--------|------:|
| .NET tests passed | **289** |
| .NET tests failed | **0** |
| TypeScript SDK unit tests | **5** (pass) |
| Line coverage (merged Cobertura) | **77.9%** (19 445 / 24 940 coverable lines) |
| Branch coverage | **45.7%** (2 803 / 6 128) |
| Method coverage | **85.5%** (1 995 / 2 332) |
| Assemblies in coverage merge | **23** |

### Per-assembly line coverage

| Assembly | Line % |
|----------|-------:|
| Morph.Identity | 100% |
| Morph.ObjectStore | 100% |
| Morph.StorageEngine | 100% |
| Morph.Functions.Runtime | 96.2% |
| Morph.Host | 95.9% |
| Morph.Infrastructure | 94.8% |
| Morph.Tenancy | 93.9% |
| Morph.Mail | 93.3% |
| Morph.Crypto | 88.0% |
| Morph.Files | 87.7% |
| Morph.Access | 84.1% |
| Morph.Plugins.Abstractions | 84.7% |
| Morph.BuildingBlocks | 82.7% |
| Morph.Client | 81.9% |
| Morph.Platform.Api | 77.6% |
| Morph.Messaging | 76.9% |
| Morph.Plugins | 73.2% |
| Morph.Runtime.Api | 72.9% |
| Morph.Sms | 71.4% |
| Morph.EntityEngine | 70.1% |
| Morph.ProcessEngine | 70.0% |
| Morph.DataPlane | 56.1% |
| morph (CLI) | 45.0% |

Coverage proves **execution reach**. Functional validity also requires the **capability matrix** in §3 (contract tests), which is the binding gate.

### How to regenerate this snapshot

```powershell
# From repo root — Docker required for Host / EntityEngine / DataPlane integration
Get-Process -Name Morph.Host -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item -Recurse -Force TestResults -ErrorAction SilentlyContinue

dotnet test Morph.sln --collect:"XPlat Code Coverage" --results-directory ./TestResults

# Merge (requires: dotnet tool install -g dotnet-reportgenerator-globaltool)
reportgenerator `
  "-reports:TestResults/**/coverage.cobertura.xml" `
  "-targetdir:TestResults/merged-coverage" `
  "-reporttypes:TextSummary;HtmlSummary;Cobertura"

Get-Content TestResults/merged-coverage/Summary.txt | Select-Object -First 20

# TypeScript SDK
cd sdks/typescript; npm test; cd ../..
```

Open `TestResults/merged-coverage/index.html` for browsable detail. Update the table above when the numbers move.

---

## 2. Validity principles

1. **Ideal contracts** — Assert the status and body Morph *should* return (e.g. `Completed`, `201`/`204`, `morph_sk_…`), not “any 2xx” or `BeOneOf` soups.
2. **Fix product or test** — If a test fails, either the product is wrong or the expectation is wrong. Do not weaken asserts to greenwash coverage.
3. **Layers stack** — Unit → integration (Postgres/MinIO) → Host HTTP e2e → architecture (NetArchTest) → SDK stubs → panel-path “practical project.”
4. **Panel = SDK = API** — The Dev Panel (`apps/admin-console`) must call only `@morph/sdk`. Host e2e covers the same routes the panel uses.
5. **No rubber stamps** — Prefer one Community Blog workflow that builds a real project over dozens of empty DTO constructors.

---

## 3. Capability matrix (binding functional claims)

Legend: **Proven** = automated test asserts ideal outcome · **Partial** = happy path or stub only · **Gap** = not yet contract-proven.

| Capability | Claim | Proof (primary) | Status |
|------------|-------|-----------------|--------|
| Health / readiness | `/health/ready` and `/health/live` succeed | `HostSmokeTests` | Proven |
| Auth register + JWT | Register → password grant → `/v1/auth/me` | `PlatformAuthAndTenancyTests`, `MorphApiHelper.BootstrapAsync` | Proven |
| Password reset / verify edges | Invalid tokens fail without leaking | `PlatformAuthAndTenancyTests` | Proven |
| Tenancy | Org / project / environment create + slug conflict | `PlatformAuthAndTenancyTests` | Proven |
| Owner bootstrap | List roles + bind `owner` for data plane | Bootstrap helper; Access e2e | Proven |
| Entity definitions | Create → publish → migrate → effective model | `PlatformDefinitionMigrationDataE2ETests`, `CommunityBlogPanelWorkflowTests` | Proven |
| Composition / decompose | Compose entities; extract field to component | `PlatformDefinitionMigrationDataE2ETests` | Proven |
| Staged migrations | Apply Completed; resume unknown → 404 | Definition + EntityEngine integration | Proven |
| Data CRUD | Create / get / patch / list / delete records | Definition e2e + Community Blog | Proven |
| Access RBAC/ABAC | Roles, bindings, policies publish; evaluator unit | `PlatformAccessCredentialsWebhooksTests`, `Morph.Access.Tests`, DataPlane vertical slice | Proven |
| API credentials | Issue (`morph_sk_…`), list, revoke | Access/credentials e2e | Proven |
| Files / object storage | Bucket → upload → complete → download → delete; content-type reject | `RuntimeFilesSqlGraphqlTests`, `Morph.Files.Tests` | Proven |
| Governed SQL | Allowlisted SELECT + params; validation errors | Runtime + DataPlane SQL tests | Proven |
| GraphQL | Auth query, introspection, `records` connection | `RuntimeFilesSqlGraphqlTests` | Proven |
| Processes | Upsert YAML → validate → publish → run → logs; delay → Waiting | `PlatformProcessFunctionSmsTests`, ProcessEngine unit | Proven |
| Functions | Register stub → list → invoke (`succeeded`) | Process/Function e2e + Functions unit | Proven (stub runtime) |
| SMS | Validation + TestSmsSender delivery | Process/Function/SMS e2e | Proven (test provider) |
| Webhooks | Create/list/delete; private URL rejected (SSRF) | Access/webhooks e2e + Messaging signature unit | Proven |
| Plugins | Catalog shape; unknown route 404; loader/permissions | Host plugins + `Morph.Plugins.Tests` | Proven |
| OpenAPI | `/openapi/v1.json` in Development | `HostPluginsAndOpenApiTests` | Proven |
| Backup export/import | Round-trip definitions | `PlatformDefinitionMigrationDataE2ETests` | Proven |
| Crypto | Project-scoped encryption; edge cases | `Morph.Crypto.Tests` | Proven |
| Architecture boundaries | Modular monolith dependency rules | `Morph.Architecture.Tests` | Proven |
| .NET SDK | HTTP clients for tenancy/data/files against stubs | `Morph.Client.Tests` | Proven (HTTP contract stubs) |
| TypeScript SDK | Auth headers, errors, login form body | `sdks/typescript` `npm test` | Proven (unit) |
| Dev Panel end-to-end path | Full panel API tour as Community Blog | `CommunityBlogPanelWorkflowTests` | Proven |
| CLI schema/plugins | Config store, YAML round-trip, command failures | `Morph.Cli.Tests` | Partial (line cov ~45%) |
| Realtime SignalR subscribe | Live hub under Host load | Messaging authorizer unit; hub mostly uncovered | Partial / Gap |
| GraphQL mutations | Write via GraphQL | Queries covered; mutations ~0% line | Gap |
| Production SMS/email providers | Live Mellipayamak / SMTP beyond Mailpit | Facades + Null/Test senders | Partial |
| Function sandbox | Real TS/DotNet isolate | TypeScriptStub / DotNetStub only | Partial |
| Multi-region / Citus | Live distributed storage | Docs + StorageEngine facade | Gap (by design for now) |

---

## 4. Dev Panel surface → proof

Every page in `apps/admin-console` maps to Host routes exercised by tests (chiefly Community Blog + focused e2e).

| Panel page | SDK / API surface | Primary proof |
|------------|-------------------|---------------|
| Login / bootstrap | register, token, org, project, env, roles, bind owner, me | Auth/tenancy + Bootstrap |
| Overview | `/health/ready`, definitions, `/plugins/v1` | Smoke + Community Blog + plugins |
| Collections | create definition, publish, plan/apply migration, effective model | Definition e2e + Community Blog |
| Records | list/create/patch/delete `/data/v1/{entity}` | Definition e2e + Community Blog |
| Access | list/create roles, list policies | Access e2e + Community Blog |
| Storage | create bucket | Community Blog + Files e2e |
| Processes | list + run (YAML upsert is CLI-equivalent seed) | Process e2e + Community Blog |
| Functions | register stub + invoke | Function e2e + Community Blog |
| API | credentials, GraphQL, governed SQL | Credentials + Runtime + Community Blog |
| Webhooks | create + list | Webhooks e2e + Community Blog |
| Settings | SMS send | SMS e2e + Community Blog |

**Practical project:** `CommunityBlogPanelWorkflowTests.Build_community_blog_through_panel_apis` builds author/post/comment collections, seeds content, creates moderator role + media bucket, runs a process, invokes a function, issues a credential, queries GraphQL/SQL, registers a webhook, and sends SMS—with strict body asserts throughout.

```powershell
dotnet test tests/Morph.Host.Integration.Tests --filter CommunityBlogPanelWorkflowTests
```

---

## 5. Test inventory by layer

| Layer | Project(s) | Count (approx.) | Role |
|-------|------------|----------------:|------|
| Architecture | `Morph.Architecture.Tests` | 12 | Dependency rules |
| Unit – domain | Access, EntityEngine, ProcessEngine, Functions, Crypto, Files, Messaging, Mail, Sms, BuildingBlocks, StorageEngine, ObjectStore, Plugins | ~140+ | Algorithms & contracts |
| Integration – Postgres | `Morph.EntityEngine.Integration.Tests`, `Morph.DataPlane.Integration.Tests` | 32 | Migrations, filters, SQL, GraphQL access, process EF |
| Host HTTP e2e | `Morph.Host.Integration.Tests` | 40 | Full stack + Testcontainers Postgres |
| SDK .NET | `Morph.Client.Tests` | 22 | Client HTTP contracts |
| CLI | `Morph.Cli.Tests` | 41 | Schema/config/commands |
| SDK TypeScript | `sdks/typescript` | 5 | Client unit |

Host e2e classes (functional spine):

- `HostSmokeTests`
- `PlatformAuthAndTenancyTests`
- `PlatformDefinitionMigrationDataE2ETests`
- `PlatformAccessCredentialsWebhooksTests`
- `PlatformProcessFunctionSmsTests`
- `RuntimeFilesSqlGraphqlTests`
- `HostPluginsAndOpenApiTests`
- `CommunityBlogPanelWorkflowTests`

---

## 6. Acceptance gates (“Morph is functionally valid”)

All must hold before calling a release **functionally valid**:

1. `dotnet test Morph.sln` → **0 failed**.
2. `dotnet test tests/Morph.Host.Integration.Tests` → **0 failed** (Docker up).
3. `CommunityBlogPanelWorkflowTests` → **pass** (panel path).
4. Merged **line coverage ≥ 75%** on coverable Morph assemblies (current floor; raise deliberately, never by rubber-stamp tests).
5. No new Host endpoint shipped without either a Host e2e assert or an explicit **Gap** row in §3.
6. Panel changes that call new SDK methods must update SDK request/response mapping **and** Community Blog (or a sibling practical project test).
7. `sdks/typescript` `npm test` → pass when SDK sources change.

Optional but recommended before marketing “parity complete”: close **Gap** rows for GraphQL mutations and Host realtime hub.

---

## 7. What coverage does *not* claim

- **Branch coverage ~46%** means many error/edge branches are still unexercised; prefer targeted cases over blanket line hunting.
- **CLI ~45%** — schema/config are strong; interactive push/publish/migrate HTTP paths are thinner.
- **DataPlane ~56%** — filters/SQL parsers are well tested; some runtime paths rely on Host e2e.
- Stub **Functions** and **test SMS** prove orchestration, not third-party sandboxes or carrier delivery.
- Admin console has **no separate browser e2e**; validity of the panel UI is via SDK+Host contracts the UI depends on.

---

## 8. Related docs

- [Competitive parity matrix](parity.md)
- [Code-first guide](code-first.md)
- [Process engine](process-engine.md)
- [GraphQL primary](graphql-primary.md)
- [Plugins / addons](addons/building-an-addon.md)

---

*Update §1 after every coverage re-run. Update §3 when a Gap becomes Proven or a new capability ships.*
