# Build without the console

Morph treats **git + CLI + SDKs** as a primary workflow. The visual **Dev Panel** (`apps/admin-console`) is optional but covers the same APIs (collections, records, access, storage, processes, functions, plugins, GraphQL/SQL, webhooks).

```powershell
cd apps/admin-console
npm install
npm run dev   # http://localhost:5173
```

## Layout

```text
my-app/
  morph.yaml
  morph/
    definitions/
      person.entity.yaml
      employment.component.yaml
      employee.entity.yaml
    policies/
      employee.viewer.policy.yaml
  src/                 # your app (uses @morph/sdk or Morph.Client)
```

## Workflow

```powershell
# 1. Dependencies + Host
cd deploy; docker compose up -d postgres redis mailpit
dotnet run --project src/Morph.Host --launch-profile http

# 2. Scaffold schema-as-code
dotnet run --project tools/Morph.Cli -- init --name my-app
dotnet run --project tools/Morph.Cli -- login --email dev@morph.local --password 'Password1!'

# Create org/project/env via SDK or HTTP, then:
dotnet run --project tools/Morph.Cli -- link --project <projectId> --env <envId>

# 3. Sync definitions from YAML → Authoring API
dotnet run --project tools/Morph.Cli -- push
dotnet run --project tools/Morph.Cli -- diff
dotnet run --project tools/Morph.Cli -- publish
dotnet run --project tools/Morph.Cli -- migrate --apply

# 4. Typed stubs + app CRUD
dotnet run --project tools/Morph.Cli -- generate --lang all
```

TypeScript app:

```ts
import { MorphClient } from "@morph/sdk";
const morph = new MorphClient({ baseUrl: "http://localhost:5100", environmentId: "…" });
await morph.login("dev@morph.local", "Password1!");
await morph.createRecord("employee", { employeeNumber: "E-1", "person.fullName": "Ada" });
```

.NET worker:

```csharp
using var client = new MorphClient(new MorphClientOptions {
    BaseAddress = new Uri("http://localhost:5100/"),
    EnvironmentId = envId
});
await client.Auth.LoginAsync("dev@morph.local", "Password1!");
await client.Data.CreateAsync("employee", new { employeeNumber = "E-1" });
```

## Round-trip

- `morph pull` writes remote published definitions/policies to `morph/`
- `morph push` creates missing definitions (compose when `componentUses` present) and policies
- Field/definition UUIDs in YAML are stable identities — keep them in git

## Gaps (MVP)

- `morph access simulate` stubs when `POST /v1/access/simulate` is missing
- `morph dev` documents Compose steps (does not auto-orchestrate Host yet)
- Definition **updates** require draft APIs (`CreateDraft`) — push skips existing slugs
