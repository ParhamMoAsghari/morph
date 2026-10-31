# GraphQL is the primary application data API

As of Phase 7, **GraphQL (`POST /graphql`)** is the preferred surface for application CRUD against published entities. REST (`/data/v1/...`) remains fully supported for platform tooling, simple clients, and backward compatibility.

## Why GraphQL primary

- One round-trip for record + related selection shapes as the schema grows
- Same **Morph.Access** (RBAC + ABAC + PBAC) and **DataPlane** path as REST — authz outcomes match
- SDKs and codegen can converge on the GraphQL contract while REST stays available

## Endpoint

| Surface | Path | Notes |
|---------|------|-------|
| GraphQL | `POST /graphql` | Hot Chocolate; requires auth + `X-Morph-Env-Id` (or `envId` / `env_id` claim) |
| REST | `/data/v1/{entity}` | Unchanged CRUD + filter/sort/page |
| Governed SQL | `POST /sql/v1/query` | Allowlisted SELECT/DML against published entity tables only |

## GraphQL operations (v1)

```graphql
query {
  record(entity: "note", id: "…") { id fields }
  records(entity: "note", filter: "{\"field\":\"title\",\"op\":\"eq\",\"value\":\"x\"}", pageSize: 50) {
    items { id fields }
    nextCursor
    hasMore
  }
}

mutation {
  createRecord(entity: "note", fields: { title: "hello" }) { id fields }
  updateRecord(entity: "note", id: "…", fields: { title: "hi" }) { id fields }
  deleteRecord(entity: "note", id: "…")
}
```

`filter` uses the same JSON AST as REST. Field masks and row-filter obligations from Morph.Access apply identically.

## Governed SQL (companion)

`POST /sql/v1/query` accepts `{ "sql": "SELECT id, title FROM note WHERE title = @title", "parameters": { "title": "hello" } }`.

- Allowlist: single `SELECT` / `INSERT` / `UPDATE` / `DELETE`
- Tables: published entity slugs or `e_{slug}` only (env schema injected)
- Denied: DDL, `COPY`, `SET ROLE`, multi-statement, `pg_catalog` abuse
- Enforced: `sql.execute` + CRUD action, row obligations, `statement_timeout`, max rows

## REST remains

Use REST when you want resource-oriented HTTP, OpenAPI meta, or minimal clients. Do not treat REST as deprecated — treat GraphQL as the default for new app integrations.
