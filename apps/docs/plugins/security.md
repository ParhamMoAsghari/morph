# Plugin security model

1. **Default deny** — every builder API checks manifest permissions.
2. **No Host references** — plugins depend on Abstractions only; ALC isolation reduces accidental coupling.
3. **SSRF guards** — outbound HTTP and webhook target URLs cannot hit private networks.
4. **Access still applies** — data client paths are meant to enforce Morph.Access (do not bypass with raw SQL).
5. **Trust boundary** — today’s model fits first-party / reviewed addons. Untrusted marketplace plugins will need stronger isolation (out-of-process workers, signatures) — see ADR-024 follow-ups.

## Operator checklist

- [ ] Permissions match actual builder calls  
- [ ] Secrets via declared schema / config refs — not hard-coded in DLL  
- [ ] Outbound calls go through `GetHttpClientFactory`  
- [ ] Inbound handlers validate signatures / shared secrets  
- [ ] Plugin folder only contains intended assemblies  

## Failure modes

| Symptom | Likely cause |
|---------|--------------|
| `PluginPermissionException` at startup | Missing permission in JSON/manifest |
| 404 on `/plugins/v1/{id}/…` | Not installed, wrong id, or route not registered |
| Empty catalog | Wrong `Plugins:Path` or empty plugins directory |
