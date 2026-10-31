# Entity engine

Responsibilities:

- Authoring model for entities/components and fields
- Versioning (draft → published)
- Composition / decomposition
- Effective model projection
- **Staged migrations** that expand/backfill/constrain storage safely

Integration tests (`Morph.EntityEngine.Integration.Tests`) exercise staged migration against Testcontainers Postgres. Host e2e covers publish → plan → apply → CRUD.

Field identities (GUIDs) remain stable across YAML round-trips for code-first workflows.
