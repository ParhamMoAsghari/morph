# Morph SDK (Kotlin) — stub

Thin client skeleton for Morph BaaS. Full OpenAPI codegen lands later.

## Auth

Send `Authorization: Bearer <access_token>` on each request. Tokens come from `POST /connect/token`.

## Base URL

Pass the Morph.Host origin into `MorphClient(baseUrl = ...)`. Trailing slashes are stripped.

## Coverage (placeholder)

- Data CRUD under `/data/v1`
- Processes under `/v1/environments/{id}/processes`
- Functions under `/v1/environments/{id}/functions`
