# Plugins & health

## Health

| Path | Meaning |
|------|---------|
| `/health/live` | Process up |
| `/health/ready` | Tagged ready checks (Postgres, etc.) |

Ready payload: `{ status, totalDuration, entries }`.

## Plugins

| Method | Path |
|--------|------|
| GET | `/plugins/v1` | Catalog `{ plugins: [ … ] }` |
| * | `/plugins/v1/{pluginId}/…` | Inbound routes registered by addons |

Example: Bale updates `POST /plugins/v1/morph.bale/updates`.

## Hangfire / OpenAPI

- Hangfire dashboard (dev): `/internal/hangfire`
- OpenAPI: `/openapi/v1.json` (Development)
