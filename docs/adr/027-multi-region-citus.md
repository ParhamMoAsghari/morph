# ADR-027: Multi-region + Citus storage backend

## Status

Accepted (2026-10-29) — architecture decision; Citus cluster not required for Phase 10.

## Context

Morph must scale tenant data and survive regional failure without dual-writing definition metadata. Postgres-per-env works for MVP; larger deployments need sharding and a documented multi-region topology.

## Decision

1. **Metadata single-writer** — definitions, Access policies, process IR, and credentials live in a primary control region; secondaries pull.
2. **Active-active data planes** — entity records and files are region-affine; routing pins writes to a home region.
3. **StorageEngine backends** — `StorageBackendKind.Postgres` (default) and `StorageBackendKind.Citus` (planned). Citus coordinator holds `platform.*`; workers shard by `project_id`.
4. **Morph.StorageEngine** — product facade over DataPlane/EntityEngine; documents backend kind and compiles filters; does not replace DataPlane.
5. **Docs** — `docs/multi-region.md` and `deploy/citus/README.md` are the operator references.

**Not chosen:** multi-master definition writes; application-level sharding without Postgres/Citus; requiring Citus for local/dev Host.

## Consequences

### Positive

- Clear RPO/RTO story for control vs data
- Path from single Postgres to Citus without rewriting Access/DataPlane contracts

### Negative / follow-ups

- Cross-region reads may be stale within RPO
- Failover runbooks and automated promotion are deferred
- `StorageBackendKind.Citus` is informational until compose/cluster wiring lands

## Roadmap placement

| When | Work |
|------|------|
| Phase 10 | Facade enum + docs + ADR |
| Later | docker-compose.citus.yml; coordinator connection routing; isolation demos |
