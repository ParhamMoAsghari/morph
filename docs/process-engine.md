# Morph Process Engine

Low-code / no-code semi-BPMS for Morph. Author processes as YAML (or JSON IR), push drafts, publish compiled graphs, and run durable instances.

## HTTP

Base path: `/v1/environments/{environmentId}/processes`

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/` | List process definitions |
| POST | `/` or `/push` | Upsert draft from YAML/JSON |
| POST | `/versions/{versionId}/publish` | Compile + publish |
| POST | `/versions/{versionId}/validate` | Compile check |
| POST | `/{slug}/run` | Start instance |
| GET | `/instances/{id}` | Instance status |
| GET | `/instances/{id}/logs` | Activity logs |

## CLI

```text
morph process push [--slug <slug>]
morph process diff [--slug <slug>] [--exit-code]
morph process publish --slug <slug>
morph process run --slug <slug> [--input '{...}']
morph process logs --instance <guid>
```

Local files live under `morph/processes/*.yaml`.

## Built-in activities (MVP)

`variables.set`, `string.*`, `json.*`, `http.request`, `morph.data.*`, `morph.functions.invoke`, `delay`, `access.assert`, plus `sequence` / `if` control flow. Plugin addons can register extra activity types via `IProcessActivityRegistry`.

## Persistence

EF Core tables in schema `platform`: `process_definitions`, `process_versions`, `process_instances`, `process_activity_logs` (migration `ProcessEngine`). Host wires `AddMorphProcessEngine` + `AddMorphProcessEnginePersistence`.
