# ADR-024: Capability-based Morph plugin / addon system

## Status

Accepted (2026-10-29)

## Context

Morph Phase 0–1 ships a fixed Host DI composition (Identity, EntityEngine, Platform/Runtime APIs). Connectors such as Bale bots, Mellipayamak SMS, and generic SOAP APIs would otherwise require forking Host or forcing every integration through Functions.

The product needs a first-class way to publish, install, and enable reusable connectors that register Process activities, inbound webhooks, and provider adapters without bypassing Morph.Access.

## Decision

Adopt **capability-based .NET plugins**:

1. **`Morph.Plugins.Abstractions`** — public contracts (`IMorphPlugin`, `PluginManifest`, `IPluginBuilder`, `ISmsProvider`, `IInboundWebhookHandler`, `IProcessActivityRegistry`).
2. **`Morph.Plugins`** — discovery from `MORPH_Plugins__Path` / `Plugins:Path` (default `./plugins`), collectible `AssemblyLoadContext` per plugin, permission gates on builder APIs, in-memory enable/config store, Host wiring via `AddMorphPlugins` / `MapMorphPlugins`.
3. **Permissions are default-deny** — undeclared `process.activities`, `sms.send`, `http.*`, `data.*`, `secrets.read` throw `PluginPermissionException`.
4. **Outbound HTTP** — plugins use host `IPluginHttpClientFactory` with SSRF guards; raw unrestricted `HttpClient` is not part of the sanctioned surface.
5. **First-party reference addons** — `Morph.Addon.Soap`, `Morph.Addon.Mellipayamak`, `Morph.Addon.Bale` under `/addons`.
6. **CLI** — `morph plugin list|install` for operator workflows; `morph.yaml` `plugins:` section for code-first projects.

**Not chosen:** arbitrary script injection without manifests; requiring Functions for every SMS/bot call; trusting plugins to call Host internals via reflection.

## Consequences

### Positive

- Extensibility without core forks
- Clear permission model and SSRF boundary for outbound calls
- Process Engine can later consume `IProcessActivityRegistry` without redesign

### Negative / follow-ups

- ALC versioning and dependency isolation need care as addon ecosystems grow
- Untrusted third-party plugins will need stronger isolation (out-of-process workers) later
- Secrets injection, Access-backed `IMorphDataClient`, and real provider HTTP calls remain stubs until Sms/Process phases land
- Signed plugin marketplace is deferred

## Roadmap placement

| When | Work |
|------|------|
| Now (post Phase 0 stubs) | Abstractions + loader + reference addons + CLI/docs |
| Phase 8 Process Engine | Activity execution path; Soap as first consumer |
| Phase 9 Sms / Functions | Mellipayamak provider selection; Bale → Process/Function triggers |
| Phase 9+ | Marketplace UX; EF-backed enable/config; signatures |
