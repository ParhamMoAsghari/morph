# Your first app — Community Blog

A practical Morph project that exercises the surfaces the Dev Panel uses. The same flow is automated in `CommunityBlogPanelWorkflowTests`.

## Goal

- Collections: `author`, `post`, `comment`
- Seed posts and comments
- Role: `moderator`
- Bucket: `media`
- Process: welcome author
- Function: slugify stub
- Credential, GraphQL, SQL, webhook, SMS

## Steps (panel or SDK)

1. **Bootstrap** — register, login, org → project → `dev` environment, bind `owner`.
2. **Collections** — for each entity, create fields → publish → plan migration → apply.
3. **Records** — create Ada as author; two posts; a comment; patch a title; delete the comment if desired.
4. **Access** — create role `moderator` inheriting `editor`.
5. **Storage** — create bucket `media`.
6. **Process** — upsert YAML via API/CLI, validate, publish, run from panel.
7. **Functions** — register `slugify-title` TypeScript stub; invoke with `{ "title": "…" }`.
8. **API** — issue `blog-ci` credential; GraphQL `records(entity: "post")`; SQL `SELECT title, slug FROM post`.
9. **Webhooks** — subscribe to `record.created` / `record.updated` on a public HTTPS URL.
10. **Settings** — send SMS to a test number (Host uses the test SMS sender in integration).

## Minimal process YAML

```yaml
apiVersion: morph/v1
kind: Process
slug: welcome-author
displayName: Welcome Author
steps:
  - id: set-greeting
    type: variables.set
    with:
      name: greeting
      value: welcome
```

```http
POST /v1/environments/{envId}/processes
{ "yaml": "<above>" }

POST /v1/environments/{envId}/processes/versions/{versionId}/validate
POST /v1/environments/{envId}/processes/versions/{versionId}/publish
POST /v1/environments/{envId}/processes/welcome-author/run
{ "input": { "author": "Ada Lovelace" } }
```

## Verify with tests

```powershell
dotnet test tests/Morph.Host.Integration.Tests --filter CommunityBlogPanelWorkflowTests
```

When this passes, the Host contracts behind the panel are green.
