# Architecture

Morph is a **modular monolith** on **.NET 9**. One Host process composes libraries with NetArchTest-enforced boundaries.

## Solution map

| Project | Responsibility |
|---------|----------------|
| `Morph.BuildingBlocks` | Result, paging, clock |
| `Morph.Identity` | Users / auth contracts |
| `Morph.Tenancy` | Org / project / environment |
| `Morph.Access` | RBAC + ABAC + PBAC evaluation |
| `Morph.EntityEngine` | Definitions, composition, staged migrations |
| `Morph.DataPlane` | Record store, filters, SQL helpers |
| `Morph.Files` / `ObjectStore` / `StorageEngine` | Buckets, uploads, storage facade |
| `Morph.Messaging` | Outbox, webhook signatures, realtime authz |
| `Morph.ProcessEngine` | YAML/IR → compile → durable run |
| `Morph.Functions.Runtime` | Function registry + stub invokers |
| `Morph.Mail` / `Morph.Sms` | Provider facades |
| `Morph.Crypto` | Project-scoped encryption |
| `Morph.Plugins` (+ Abstractions) | ALC loader, permission gates |
| `Morph.Infrastructure` | EF, OpenIddict, Hangfire, OTel, health |
| `Morph.Platform.Api` / `Morph.Runtime.Api` | HTTP endpoint maps |
| `Morph.Host` | Composition root |

## Read next

- [System design](/architecture/system)
- [Entity engine](/architecture/entity-engine)
- [Access](/architecture/access)
- [Process engine](/architecture/process-engine)
- [Storage & crypto](/architecture/storage-crypto)
- [Hosting & config](/architecture/hosting)
- [Multi-region](/architecture/multi-region)
