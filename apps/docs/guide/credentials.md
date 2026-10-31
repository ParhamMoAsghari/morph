# API credentials

Machine keys for CI and backends.

```http
POST /v1/projects/{projectId}/credentials
{ "name": "ci-key", "environmentId": "<env-id>" }
```

Response includes a one-time **`secret`** starting with `morph_sk_`. Store it securely; Morph keeps a hash.

```http
GET  /v1/projects/{projectId}/credentials
POST /v1/projects/{projectId}/credentials/{id}/revoke
```

Use the secret as an API key with the SDK (`apiKey` option) or documented header conventions for automation.
