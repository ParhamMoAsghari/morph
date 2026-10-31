# Tenancy

## Create org, project, environment

```http
POST /v1/orgs
{ "name": "Acme", "slug": "acme" }

POST /v1/projects
{ "organizationId": "<org>", "name": "App", "slug": "app" }

POST /v1/projects/{projectId}/environments
{ "name": "Development", "slug": "dev" }
```

Slugs must be unique where required — duplicate org slug returns conflict.

## Bind access for yourself

```http
POST /v1/projects/{projectId}/role-bindings
{
  "principalId": "<user-guid>",
  "roleName": "owner",
  "environmentId": "<env-guid>"
}
```

List roles first (`GET /v1/projects/{id}/roles`) so built-ins are ensured.

## SDK

```ts
const org = await morph.createOrganization("Acme", "acme");
const project = await morph.createProject(org.id, "App", "app");
const env = await morph.createEnvironment(project.id, "Development", "dev");
morph.setProjectId(project.id);
morph.setEnvironmentId(env.id);
await morph.bindRole(userId, "owner", env.id);
```
