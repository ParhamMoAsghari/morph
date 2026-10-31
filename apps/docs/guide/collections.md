# Collections & schema

Collections are **entity definitions** with typed fields. Morph stores a versioned authoring model and migrates physical storage when you apply a migration.

## Create → publish → migrate

```ts
const created = await morph.createDefinition({
  slug: "post",
  displayName: "Post",
  kind: "Entity",
  fields: [
    {
      slug: "title",
      displayName: "Title",
      dataType: "String",
      isRequired: true,
      isIndexed: true,
    },
    { slug: "body", displayName: "Body", dataType: "String" },
  ],
}) as { versionId: string; definitionId: string };

await morph.publishDefinition(created.versionId);
await morph.planMigration(created.versionId);
await morph.applyMigration(created.versionId);

const model = await morph.getEffectiveModel(created.versionId);
```

HTTP equivalents live under `/v1/environments/{environmentId}/definitions` and `/migrations/plan|apply` with body `{ "toVersionId": "…" }`.

## Field tips

- Prefer stable **slugs** — they become JSON field keys and SQL column identifiers.
- Mark **indexed** fields you will filter on frequently.
- Composition: entities can reference **components**; use compose/decompose APIs for shared shapes.

## Code-first YAML

See [CLI](/guide/cli) — definitions under `morph/definitions/*.yaml`, then `morph push` → `publish` → `migrate --apply`.

## Backup

```http
GET /v1/environments/{envId}/backup/definitions
```

Export/import round-trip is covered in Host e2e.
