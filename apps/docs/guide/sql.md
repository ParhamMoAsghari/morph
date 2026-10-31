# Governed SQL

```http
POST /sql/v1/query
X-Morph-Env-Id: …
Authorization: Bearer …

{
  "sql": "SELECT title, slug FROM post ORDER BY title"
}
```

## Rules of the road

- Allowlisted / parsed SQL only — not arbitrary DBA access
- Access checks and row caps apply
- Prefer parameterized forms when available (Host e2e covers params)

## SDK / Panel

```ts
await morph.sql("SELECT title, slug FROM post ORDER BY title");
```

Use the Dev Panel **API** page to experiment safely in Development.
