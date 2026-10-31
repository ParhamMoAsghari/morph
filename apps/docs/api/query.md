# GraphQL & SQL API

## GraphQL

```http
POST /graphql
Authorization: Bearer …
X-Morph-Env-Id: …
Content-Type: application/json

{ "query": "{ __typename }", "variables": {} }
```

## Governed SQL

```http
POST /sql/v1/query
Authorization: Bearer …
X-Morph-Env-Id: …
Content-Type: application/json

{ "sql": "SELECT title FROM post" }
```

Response includes row data and `rowCount` (shape covered by Host e2e).
