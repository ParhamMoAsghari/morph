# System design

```text
┌─────────────┐   ┌──────────────┐   ┌─────────────┐
│ Dev Panel   │   │ CLI / SDKs   │   │ Plugins     │
│ @morph/sdk  │   │              │   │ Abstractions  │
└──────┬──────┘   └──────┬───────┘   └──────┬──────┘
       │                 │                  │
       └────────────┬────┴──────────────────┘
                    ▼
             ┌─────────────┐
             │ Morph.Host  │  OpenIddict · Hangfire · Health · CORS
             └──────┬──────┘
        ┌───────────┼───────────┐
        ▼           ▼           ▼
 Platform.Api  Runtime.Api  Plugins map
        │           │
        ▼           ▼
 Infrastructure (EF Postgres, Redis, MinIO, Mail)
        │
   Engines: Entity · Access · Data · Process · Functions · Files · Messaging
```

## Request paths

- **Platform** — auth, tenancy, definitions, access, processes, functions, SMS, credentials, webhooks
- **Runtime** — `/data/v1`, `/files/v1`, `/graphql`, `/sql/v1/query`, SignalR hub
- **Plugins** — `/plugins/v1/{pluginId}/…`

## Persistence

PostgreSQL holds platform schema (Identity, tenancy, definitions, processes, credentials, …) and hybrid entity storage (tables + JSONB strategies via EntityEngine / DataPlane). Redis supports caching/jobs as configured. MinIO holds file blobs when enabled.
