# Processes

Low-code workflows authored as YAML (or JSON IR), published as compiled graphs, run as durable instances.

## HTTP

Base: `/v1/environments/{environmentId}/processes`

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/` | List definitions |
| POST | `/` or `/push` | Upsert draft from YAML/JSON |
| POST | `/versions/{versionId}/validate` | Compile check |
| POST | `/versions/{versionId}/publish` | Publish |
| POST | `/{slug}/run` | Start instance `{ "input": { … } }` |
| GET | `/instances/{id}` | Status |
| GET | `/instances/{id}/logs` | Activity logs |

## Example

```yaml
apiVersion: morph/v1
kind: Process
slug: greet
displayName: Greet
steps:
  - id: set
    type: variables.set
    with:
      name: message
      value: ok
```

`variables.set`-only processes complete synchronously (`status: Completed`). A `delay` step yields `Waiting`.

## Built-in activities (MVP)

`variables.set`, `string.*`, `json.*`, `http.request`, `morph.data.*`, `morph.functions.invoke`, `delay`, `access.assert`, plus `sequence` / `if`. Plugins register more via `IProcessActivityRegistry`.

## CLI

```text
morph process push
morph process publish --slug greet
morph process run --slug greet --input "{\"hello\":\"world\"}"
morph process logs --instance <guid>
```

Files: `morph/processes/*.yaml`.

## Panel

Processes page **lists and runs** published processes. Push YAML with CLI or API first.
