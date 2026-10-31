# Records (Data API)

Base path: **`/data/v1/{entitySlug}`**  
Requires: Bearer (or credential) + **`X-Morph-Env-Id`**.

## CRUD

| Method | Path | Notes |
|--------|------|-------|
| GET | `/data/v1/{entity}` | Paged list (`pageSize`, filters) |
| POST | `/data/v1/{entity}` | Create; body = field map |
| GET | `/data/v1/{entity}/{id}` | Get one |
| PATCH | `/data/v1/{entity}/{id}` | Partial update |
| DELETE | `/data/v1/{entity}/{id}` | Typically **204** |

Response records include `id`, `entity`, and `fields`.

## SDK

```ts
await morph.createRecord("post", { title: "Hi", body: "…" });
const page = await morph.listRecords("post", { pageSize: "50" });
await morph.updateRecord("post", id, { title: "Updated" });
await morph.deleteRecord("post", id);
```

## Meta

```http
GET /data/v1/_meta/status
```

Expect `{ "status": "ready" }` when the data plane is up.

## Access

Unauthorized → 401. Authenticated without sufficient role/policy → **403**. Bind `owner` during bootstrap for local admin work.
