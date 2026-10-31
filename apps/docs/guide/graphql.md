# GraphQL

Hot Chocolate endpoint: **`POST /graphql`**  
Requires auth + environment header (same as data plane).

## Introspection

```graphql
{
  __schema {
    queryType { name }
    mutationType { name }
  }
}
```

Query type name includes Morph’s GraphQL surface (e.g. `MorphGraphqlQueries`).

## List records

```graphql
query ($entity: String!) {
  records(entity: $entity, pageSize: 10) {
    items { id entity }
    hasMore
  }
}
```

Variables: `{ "entity": "post" }`

## SDK

```ts
await morph.graphql(
  `query ($entity: String!) {
     records(entity: $entity, pageSize: 10) {
       items { id entity }
       hasMore
     }
   }`,
  { entity: "post" },
);
```

## Notes

- GraphQL is a **primary** query surface alongside REST; see architecture docs.
- Mutations coverage is thinner than queries today — prefer REST writes when in doubt, or track the Gap in [Testing](/reference/testing).
