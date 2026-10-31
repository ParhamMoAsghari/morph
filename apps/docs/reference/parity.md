# Competitive parity

Morph tracks capability parity against reference BaaS products while keeping **native** APIs (not protocol clones).

| Capability | Morph posture |
|------------|---------------|
| Auth (email/password, JWT) | Partial → expanding |
| Row-level / policy access | Yes (RBAC+ABAC+PBAC) |
| Dynamic collections | Yes (entity engine) |
| REST data API | Yes |
| GraphQL | Yes (primary query surface) |
| Governed SQL | Yes |
| Files + signed URLs | Yes |
| Realtime | Partial (SignalR + Access) |
| Webhooks | Yes (signed) |
| Jobs | Yes (Hangfire + outbox) |
| Email / SMS | Yes / Partial (plugins) |
| Functions | Partial (stubs) |
| Low-code processes | Yes / Partial |
| Schema-as-code CLI | Yes |
| Plugins | Yes (capability-based) |

Canonical table: repo `docs/parity.md`.
