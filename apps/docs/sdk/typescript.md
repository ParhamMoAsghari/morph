# TypeScript — `@morph/sdk`

## Install (local workspace)

```powershell
cd sdks/typescript
npm install
npm run build
```

Link from apps via workspace / file dependency (admin-console already references it).

## Construct

```ts
import { MorphClient } from "@morph/sdk";

const morph = new MorphClient({
  baseUrl: "http://localhost:5100",
  projectId: "<guid>",
  environmentId: "<guid>",
  // optional: apiKey, accessToken, fetch, platformClientId/Secret
});

await morph.login("dev@morph.local", "Password1!");
```

## Common methods

| Area | Methods |
|------|---------|
| Auth | `register`, `login`, `me` |
| Tenancy | `createOrganization`, `createProject`, `createEnvironment`, `bindRole` |
| Schema | `listDefinitions`, `createDefinition`, `publishDefinition`, `planMigration`, `applyMigration`, `getEffectiveModel` |
| Data | `listRecords`, `createRecord`, `getRecord`, `updateRecord`, `deleteRecord` |
| Query | `graphql`, `sql` |
| Files | `createBucket` |
| Access | `listRoles`, `createRole`, `listPolicies` |
| Automation | `listProcesses`, `runProcess`, `listFunctions`, `registerFunction`, `invokeFunction` |
| Ops | `listCredentials`, `issueCredential`, `listWebhooks`, `createWebhook`, `listPlugins`, `sendSms`, `healthReady` |

## Body mapping notes

The SDK normalizes a few Platform DTO names for ergonomics:

- Roles: `name` + `displayName` (+ `inherits`)
- Webhooks: sends `targetUrl` / `eventTypes`; returns `url` / `events`
- SMS: sends `body` (and `message` alias)
- Function invoke: wraps payload as `{ input }`
- Plugins list: unwraps `{ plugins: […] }`
