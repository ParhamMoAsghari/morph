# Competitive parity matrix (stub)

Tracking Morph capability coverage vs reference BaaS products. Morph APIs remain native (not protocol clones). Kernel quality gates parity work.

| Capability | Morph | Supabase | Appwrite | PocketBase | Parse | Notes |
|------------|-------|----------|----------|------------|-------|-------|
| Auth (email/password, JWT) | Partial | Yes | Yes | Yes | Yes | OpenIddict + Identity; social/MFA later |
| Row-level / policy access | Yes (RBAC+ABAC+PBAC) | RLS | Permissions | Rules | CLPs | Unified Morph.Access |
| Dynamic DB / collections | Yes (entity engine) | Postgres | Collections | Collections | Classes | Composition is Morph differentiator |
| REST data API | Yes | Yes | Yes | Yes | Yes | Phase 3 |
| GraphQL | Yes (primary) | Yes | No | No | No | Hot Chocolate `/graphql`; REST remains |
| Governed SQL | Yes | Yes | No | No | No | `POST /sql/v1/query`; allowlist + Access + row caps |
| File storage + signed URLs | Yes | Yes | Yes | Yes | Yes | MinIO/S3 Phase 4 |
| Realtime subscriptions | Yes (SignalR) | Yes | Yes | Yes | LiveQuery | Phase 6; Access on subscribe |
| Webhooks (signed) | Yes | Yes | Yes | Plugins | Cloud Code | HMAC `X-Morph-Signature` |
| Background jobs | Yes (Hangfire+outbox) | Edge/cron | Functions | — | Cloud Jobs | |
| Email | Yes (SMTP) | Yes | Yes | — | Yes | Morph Mail product later |
| SMS | Partial (stub facade) | Yes | Yes | — | Yes | `/v1/sms`; plugin providers (e.g. Mellipayamak); Null sender without plugin |
| Functions / cloud code | Partial (invoke stubs) | Edge Fn | Functions | — | Cloud Code | Register + echo invoke; no real sandbox yet |
| Low-code workflows | Yes / Partial | — | — | — | — | Process Engine IR + builtins; durable delay; Functions invoke port |
| Schema-as-code / CLI | Yes | Migrations | — | — | — | Phase 5 `morph` CLI |
| Quotas / usage | Soft (Phase 6) | Yes | Yes | Soft | — | Hard limits + billing later |
| Audit log | Minimal (Phase 6) | Yes | Yes | — | — | Security-sensitive ops |
| Backup / export | Definitions JSON | Yes | Yes | Export | Export | Full project backup later |
| Multi-region / Citus | Partial (docs + StorageEngine facade) | Yes | Regions | — | — | See docs/multi-region.md; Citus notes under deploy/citus |

**Critical path for “parity complete” (product gate):** auth, DB, storage, realtime, functions-or-jobs, webhooks — see plan Phase 9 acceptance.
