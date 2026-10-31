# HTTP API overview

Morph.Host exposes Platform APIs (auth, tenancy, definitions, access, processes, …) and Runtime APIs (data, files, GraphQL, SQL, realtime).

## Base URL

Local Development: `http://localhost:5100`

Root `GET /` returns a discovery document (health, graphql, sql, data, processes, functions, sms, realtime, admin console hint).

## Auth

| Mode | How |
|------|-----|
| User JWT | `Authorization: Bearer <access_token>` from `/connect/token` |
| API credential | Project credential secret (`morph_sk_…`) — see credentials guide |

## Environment header

Runtime APIs require:

```http
X-Morph-Env-Id: <environment-guid>
```

Platform routes that take `{environmentId}` in the path still use the JWT; data/files/graphql/sql also need the header.

## OpenAPI

In Development:

```text
GET /openapi/v1.json
```

## Sections

- [Auth](/api/auth)
- [Platform](/api/platform)
- [Data plane](/api/data)
- [Files](/api/files)
- [GraphQL & SQL](/api/query)
- [Processes & functions](/api/automation)
- [Plugins & health](/api/ops)
- [Errors & headers](/api/conventions)
