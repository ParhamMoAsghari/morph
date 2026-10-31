# ADR-023: Morph Process Engine — low-code/no-code semi-BPMS

## Status

Accepted (2026-10-29)

## Context

Automations need both no-code orchestration and code hooks. Functions alone force developers to write glue; a BPMS-style engine covers HTTP, data, transforms, and branching with durable runs.

## Decision

Ship **Morph.ProcessEngine** with:

1. YAML/JSON IR + versioned publish (aligned with entity lifecycle).
2. Built-in activity catalog (variables, string/json, HTTP with SSRF guards, Morph Data CRUD, delay, access.assert, morph.functions.invoke).
3. Durable instance + activity log persistence via EF (`process_*` tables).
4. HTTP `/v1/environments/{id}/processes` and CLI `morph process …`.
5. Plugin activities via `IProcessActivityRegistry`.

## Consequences

- Processes and Functions are complementary (orchestrate vs embed code).
- Visual designer can share the same IR later.
- Full Elsa substrate optional; Morph-owned runner is authoritative for MVP.
