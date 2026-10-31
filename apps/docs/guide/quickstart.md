# Quickstart

Get Morph running and create your first secured record in about ten minutes.

## 1. Start dependencies

```powershell
cd deploy
docker compose up -d postgres redis mailpit
```

Optional object storage:

```powershell
docker compose --profile storage up -d
```

Copy `.env.example` if you need Compose overrides. Do not commit real secrets.

## 2. Run the Host

```powershell
cd ..
dotnet run --project src/Morph.Host --launch-profile http
```

On boot the Host applies EF migrations and seeds the `morph-platform` OpenIddict client.

Confirm:

```text
GET http://localhost:5100/health/ready
```

## 3. Register and get a JWT

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
$token = Invoke-RestMethod -Method Post -Uri http://localhost:5100/connect/token -Body $body
$headers = @{ Authorization = "Bearer $($token.access_token)" }
```

## 4. Create tenancy + bind owner

```powershell
$org = Invoke-RestMethod -Method Post -Uri http://localhost:5100/v1/orgs `
  -Headers $headers -ContentType "application/json" `
  -Body '{"name":"Demo Org","slug":"demo-org"}'

$project = Invoke-RestMethod -Method Post -Uri http://localhost:5100/v1/projects `
  -Headers $headers -ContentType "application/json" `
  -Body (@{ organizationId = $org.id; name = "Demo"; slug = "demo" } | ConvertTo-Json)

$env = Invoke-RestMethod -Method Post -Uri "http://localhost:5100/v1/projects/$($project.id)/environments" `
  -Headers $headers -ContentType "application/json" `
  -Body '{"name":"Development","slug":"dev"}'

$me = Invoke-RestMethod -Uri http://localhost:5100/v1/auth/me -Headers $headers

Invoke-RestMethod -Method Post -Uri "http://localhost:5100/v1/projects/$($project.id)/role-bindings" `
  -Headers $headers -ContentType "application/json" `
  -Body (@{ principalId = $me.id; roleName = "owner"; environmentId = $env.id } | ConvertTo-Json)
```

Without the **owner** binding, data plane calls return **403**.

## 5. Publish a collection and write a record

Use the [Dev Panel](/guide/dev-panel) **Collections** page, or TypeScript:

```ts
import { MorphClient } from "@morph/sdk";

const morph = new MorphClient({
  baseUrl: "http://localhost:5100",
  projectId: "<project-guid>",
  environmentId: "<env-guid>",
});
await morph.login("dev@morph.local", "Password1!");

const created = await morph.createDefinition({
  slug: "post",
  displayName: "Post",
  kind: "Entity",
  fields: [
    { slug: "title", displayName: "Title", dataType: "String", isRequired: true, isIndexed: true },
    { slug: "body", displayName: "Body", dataType: "String" },
  ],
}) as { versionId: string };

await morph.publishDefinition(created.versionId);
await morph.planMigration(created.versionId);
await morph.applyMigration(created.versionId);

await morph.createRecord("post", { title: "Hello Morph", body: "First post" });
const page = await morph.listRecords("post");
console.log(page.items);
```

## 6. Open the Dev Panel (optional)

```powershell
cd apps/admin-console
npm install
npm run dev
```

Browse http://127.0.0.1:5173 — login with the same user, paste project/env IDs (or use bootstrap on first login).

## Next

- [Your first app](/guide/first-app) — Community Blog shaped walkthrough  
- [Concepts](/guide/concepts) — tenancy, definitions, environments  
- [Plugin quickstart](/plugins/quickstart) — ship an addon  
