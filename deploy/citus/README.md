# Citus deployment notes

Morph.StorageEngine defaults to **Postgres**. Citus is the scale-out backend:

- **Coordinator:** platform schema (`platform.*`), OpenIddict, definition metadata, process definitions.
- **Workers:** environment data schemas / entity tables; shard by `project_id` (or `tenant_id`).
- **Morph.Host:** talk to coordinator connection string; Citus routes shard queries.

## Suggested compose sketch

```yaml
# deploy/citus/docker-compose.citus.yml (operator-owned)
# services:
#   citus-coordinator:
#     image: citusdata/citus:12
#   citus-worker-1:
#     image: citusdata/citus:12
```

Wire `Morph:ConnectionStrings:Platform` to the coordinator. Enable `StorageBackendKind.Citus` via `AddMorphStorageEngine(StorageBackendKind.Citus)` when ready.

## Isolation demo

1. Two projects → two shard placements.
2. Query with `project_id` filter; confirm no cross-shard leakage in governed SQL / Access paths.
