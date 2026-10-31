# ADR-025: Morph Functions runtime

## Status

Accepted (2026-10-29) — product ADR; runtime scaffolding may land alongside Phase 9/10 hosts.

## Context

Customers need short-lived, project-scoped code hooks (HTTP triggers, process activities, webhooks) without forking Morph.Host. A Functions surface must share Morph.Access, secrets, and DataPlane clients rather than becoming a second platform.

## Decision

Adopt **Morph.Functions** as an in-process (and later out-of-process) runtime:

1. **Definitions** — versioned function metadata (slug, runtime, entry, triggers) stored with other Morph definitions.
2. **Runtime** — sandboxed execution host (`Morph.Functions.Runtime`) with allowlisted APIs: Access-gated data client, HTTP via SSRF-guarded factory, secrets read, process invoke.
3. **Triggers** — HTTP routes under Runtime API, Process activity `morph.functions.invoke`, and webhook/plugin hand-offs.
4. **Not chosen** — arbitrary unrestricted CLR plugins without manifests; forcing all automation through Process Engine only.

## Consequences

### Positive

- Clear separation: Process Engine orchestrates; Functions embed code
- Shared Access / tenancy boundaries with the rest of Morph

### Negative / follow-ups

- Isolation hardening (process/container workers) is required before untrusted tenant code
- Cold-start and scale-out workers are deferred
- Language SDKs beyond C# remain future work

## Roadmap placement

| When | Work |
|------|------|
| Phase 9 stubs | Registry + invoke ports; Process activity hook |
| Phase 10+ | Host wiring polish; docs; ADR |
| Later | Out-of-process workers; marketplace signing |
