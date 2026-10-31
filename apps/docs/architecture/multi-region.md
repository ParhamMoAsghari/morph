# Multi-region notes

Morph documents a path toward multi-region / Citus-style distribution. Today:

- Single-region Postgres + optional MinIO is the supported local/prod-shaped default
- `Morph.StorageEngine` facade and ADR-027 capture intent
- Treat multi-region as **Partial** in the validity matrix until live distributed tests exist

Read repo files `docs/multi-region.md` and `docs/adr/027-multi-region-citus.md` for design notes under `deploy/citus`.
