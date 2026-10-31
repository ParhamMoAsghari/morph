# Morph SDK (Python) — stub

Thin client skeleton for Morph BaaS. Full OpenAPI codegen lands later.

## Auth

Pass `access_token=` or set `client.access_token`. Requests send `Authorization: Bearer …`.

## Base URL

```python
from morph_sdk import MorphClient

client = MorphClient("https://api.example.com", access_token="…")
```

Placeholder methods cover data CRUD, processes, and functions.
