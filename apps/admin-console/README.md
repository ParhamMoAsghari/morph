# Morph Dev Panel

PocketBase / Supabase-style developer console for Morph. Built with Vite + React on `@morph/sdk`.

## Capabilities

| Section | Morph surface |
|---------|----------------|
| Overview | Health, env summary |
| Collections | Entity definitions, publish, effective model |
| Records | Dynamic CRUD |
| Access | Roles & policies (RBAC/PBAC) |
| Storage | File buckets |
| Processes | Process Engine runs |
| Functions | Register / invoke |
| Plugins | Installed addons |
| API & SQL | Credentials, GraphQL, governed SQL |
| Webhooks | Outbound hooks |
| Settings | Project/env scope, SMS |

## Run

```powershell
# Terminal 1 — API
cd deploy
docker compose up -d postgres redis mailpit
dotnet run --project ..\src\Morph.Host --launch-profile http

# Terminal 2 — panel
cd apps\admin-console
npm install
npm run dev
```

Open http://localhost:5173

- Sign in with an existing user, **or** use **Register + bootstrap project** to create org/project/env.
- Host CORS allows `localhost:5173`. Vite also proxies API paths when `baseUrl` is left as the Host URL, or you can point `baseUrl` at `http://localhost:5100` directly.

## Notes

- The panel uses only public Morph HTTP APIs via `@morph/sdk` (no private Host endpoints).
- Code-first CLI remains first-class; this UI is the optional visual companion.
