# Multi-region Morph

Morph targets **active-active data planes** with a **single-writer primary region for definition metadata**.

## Control vs data plane

| Plane | Ownership | Conflict policy |
|-------|-----------|-----------------|
| Definitions, policies, process IR, credentials | Global control / primary region | Single-writer primary; other regions pull |
| Entity records, files, process instances | Region-local data plane | Region-affine; async replicate or pin |
| Auth tokens | Issued locally; validate via shared issuer keys | Short-lived access tokens |

## Routing sketch

1. Edge/global DNS or LB steers clients to the nearest Morph.Host.
2. Host resolves `project_id` → home region (or pin) for writes.
3. Cross-region reads may be stale within documented RPO.

## RPO / RTO (targets)

- **Metadata (definitions):** RPO ≈ 0 within primary; secondary regions lag on pull interval (minutes initially).
- **Records:** RPO depends on replication mode (async: seconds–minutes; pin: local only until failover).
- **RTO:** promote secondary control region with runbook; do not dual-write definitions.

## Citus

See [deploy/citus/README.md](../deploy/citus/README.md). Shard tenant data by `project_id`; keep platform schema on the coordinator.
