# Dev Panel

The Dev Panel is Morph’s browser console (`apps/admin-console`). It is **SDK-only**: every action goes through `@morph/sdk` to Morph.Host — the same contracts your apps use.

## Run

```powershell
# Host must already be on :5100
cd apps/admin-console
npm install
npm run dev
```

Open http://127.0.0.1:5173 (Vite binds `127.0.0.1` to avoid IPv6-only listen issues).

## Pages

| Page | What you do |
|------|-------------|
| **Login** | Register/login, create org/project/env, bind `owner` |
| **Overview** | Health, definition count, plugins catalog |
| **Collections** | Create entity, publish, plan/apply migration, inspect effective model |
| **Records** | List/create JSON records for a published entity |
| **Access** | List roles/policies; create roles |
| **Storage** | Create file buckets |
| **Processes** | List + run published processes |
| **Functions** | Register stub functions + invoke |
| **API** | Issue credentials; try GraphQL and governed SQL |
| **Webhooks** | Create outbound webhook subscriptions |
| **Settings** | SMS send (test provider in Dev) |
| **Plugins** | View loaded plugin catalog |

## Session

The panel stores `baseUrl`, access token, `projectId`, and `environmentId` in browser session storage. Runtime calls send `Authorization: Bearer …` and `X-Morph-Env-Id`.

## Tips

- Create collections **before** records — unpublished entities have no migrated storage.
- Bind **owner** (or an editor role with write policies) or Records stay empty / 403.
- Process **YAML push** is CLI/API; the panel **runs** existing published processes.
- Functional proof of the panel path: Host test `CommunityBlogPanelWorkflowTests` (see [Testing](/reference/testing)).
