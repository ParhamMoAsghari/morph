# Processes & functions API

## Processes

`/v1/environments/{envId}/processes`

| Method | Path |
|--------|------|
| GET | `/` |
| POST | `/` or `/push` `{ yaml }` |
| POST | `/versions/{versionId}/validate` |
| POST | `/versions/{versionId}/publish` |
| GET | `/versions/{versionId}` |
| GET | `/versions/{versionId}/authoring` |
| GET | `/{definitionId}` |
| POST | `/{slug}/run` `{ input }` |
| GET | `/instances/{instanceId}` |
| GET | `/instances/{instanceId}/logs` |

## Functions

`/v1/environments/{envId}/functions`

| Method | Path |
|--------|------|
| GET | `/` |
| POST | `/` register |
| POST | `/{slug}/invoke` `{ input }` |
