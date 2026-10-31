# Morph SDK (Swift) — stub

Thin client skeleton for Morph BaaS. Full OpenAPI codegen lands later.

## Auth

Send `Authorization: Bearer <access_token>` (or API key when configured). Obtain tokens via `POST /connect/token`.

## Base URL

Point `MorphClient` at your Morph.Host origin (e.g. `https://api.example.com`). Trailing slashes are normalized.

## Coverage (placeholder)

- Data CRUD: `/data/v1/...`
- Processes: `/v1/environments/{id}/processes`
- Functions: `/v1/environments/{id}/functions`

Do not treat this package as production-ready until generated clients ship.
