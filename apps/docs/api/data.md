# Data plane API

Prefix: `/data/v1`  
Headers: `Authorization`, `X-Morph-Env-Id`

| Method | Path | Success |
|--------|------|---------|
| GET | `/data/v1/_meta/status` | `{ status: "ready" }` |
| GET | `/data/v1/{entity}` | Page `{ items, … }` |
| POST | `/data/v1/{entity}` | Created record |
| GET | `/data/v1/{entity}/{id}` | Record |
| PATCH | `/data/v1/{entity}/{id}` | Updated record |
| DELETE | `/data/v1/{entity}/{id}` | **204 No Content** |

Request body for create/patch is a JSON object of **field slug → value** (not wrapped in `fields`).
