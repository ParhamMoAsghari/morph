# Platform API

## Tenancy

| Method | Path |
|--------|------|
| POST | `/v1/orgs` |
| POST | `/v1/projects` |
| POST | `/v1/projects/{projectId}/environments` |

## Definitions & migrations

| Method | Path |
|--------|------|
| GET/POST | `/v1/environments/{envId}/definitions` |
| GET | `/v1/environments/{envId}/definitions/{definitionId}` |
| GET | `/v1/environments/{envId}/definitions/versions/{versionId}` |
| GET | `…/versions/{versionId}/effective-model` |
| POST | `…/versions/{versionId}/publish` |
| POST | `…/versions/{versionId}/validate` |
| POST | `/v1/environments/{envId}/migrations/plan` body `{ toVersionId }` |
| POST | `/v1/environments/{envId}/migrations/apply` body `{ toVersionId }` |
| GET | `/v1/environments/{envId}/backup/definitions` |

Compose / decompose / draft endpoints exist for component workflows (see Host e2e).

## Access

| Method | Path |
|--------|------|
| GET/POST | `/v1/projects/{id}/roles` |
| GET/POST | `/v1/projects/{id}/role-bindings` |
| GET/POST | `/v1/projects/{id}/policies` |
| POST | `/v1/projects/{id}/policies/{policyId}/publish` |

## Credentials & webhooks

| Method | Path |
|--------|------|
| GET/POST | `/v1/projects/{id}/credentials` |
| POST | `/v1/projects/{id}/credentials/{credentialId}/revoke` |
| GET/POST | `/v1/projects/{id}/webhooks` |
| DELETE | `/v1/projects/{id}/webhooks/{subscriptionId}` |

## SMS

| Method | Path |
|--------|------|
| POST | `/v1/sms/send` body `{ to, body }` |
