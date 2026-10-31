# Errors & headers

## Headers

| Header | When |
|--------|------|
| `Authorization: Bearer …` | User or service JWT |
| `X-Morph-Env-Id` | Data, files, GraphQL, SQL, many runtime paths |
| `Content-Type: application/json` | JSON bodies |
| `Content-Type: application/x-www-form-urlencoded` | `/connect/token` |

## Error shapes

- Validation → ProblemDetails / validation problem (`400`)
- Unauthorized → `401`
- Forbidden (Access) → `403`
- Not found → `404`
- Conflict (e.g. duplicate slug) → `409`

SDK throws `MorphApiError` with `status` and raw `body`.

## JSON

Host configures camelCase enums as strings and omits nulls when writing JSON.
