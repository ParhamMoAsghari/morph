# Access engine

`Morph.Access` evaluates whether a principal may perform an **action** on a **resource** under **conditions**.

- **RBAC** — roles and inheritance (`owner` / `editor` / `viewer` / custom)
- **Bindings** — principal ↔ role (scoped by project/environment)
- **Policies** — allow/deny with priority and optional JSON conditions (ABAC/PBAC)

Data plane, GraphQL, SQL, and realtime authorizers consult Access. Plugins that use `GetDataClient` are expected to inherit the same enforcement.

Unit tests cover evaluator, condition parsing, and hierarchy; Host e2e covers role/policy HTTP flows.
