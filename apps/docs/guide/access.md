# Access control

Morph.Access unifies **RBAC**, **ABAC**, and **PBAC**.

## Roles

```http
GET  /v1/projects/{projectId}/roles
POST /v1/projects/{projectId}/roles
{
  "name": "moderator",
  "displayName": "Moderator",
  "inherits": ["editor"]
}
```

Built-ins include `owner`, `editor`, `viewer`.

## Bindings

```http
POST /v1/projects/{projectId}/role-bindings
{
  "principalId": "<user-id>",
  "roleName": "editor",
  "environmentId": "<env-id>"
}
```

## Policies

```http
POST /v1/projects/{projectId}/policies
{
  "name": "allow-read-note",
  "effect": "Allow",
  "actions": ["read", "list"],
  "resources": ["entity:note"],
  "priority": 10,
  "environmentId": "<env-id>"
}

POST /v1/projects/{projectId}/policies/{policyId}/publish
```

Conditions can express attribute checks (JSON condition trees — see Access unit tests / parser).

## Dev Panel

Access page lists roles/policies and creates roles via the SDK (`createRole(name, displayName)`).
