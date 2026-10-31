# ADR-026: Morph.Crypto field / envelope encryption

## Status

Accepted (2026-10-29)

## Context

`FieldDefinition.Encrypt` needs a concrete helper so sensitive string/binary fields can be stored as ciphertext without inventing algorithms. Operators also need envelope helpers for secrets-at-rest adjacent to Data Protection.

## Decision

Ship **Morph.Crypto** using **NSec (libsodium) XChaCha20-Poly1305**:

1. **`IMorphCrypto` / `MorphCryptoService`** — Encrypt/Decrypt `byte[]` and `string` with **project-scoped** keys.
2. **Key derivation** — HKDF-SHA256 from a configured 32-byte master key (`Morph:Crypto:MasterKeyBase64`); development stub only when unset.
3. **Field markers** — `EncryptField` / `DecryptField` store `morph:enc:v1:<base64url>` so plain values remain distinguishable.
4. **DI** — `AddMorphCrypto()` from Infrastructure alongside Files/DataPlane.
5. **Not chosen** — custom ciphers; per-row keys without KMS; encrypting indexed columns without searchable-encryption design.

ASP.NET Data Protection remains for cookies/keys; Morph.Crypto is the product field/envelope helper.

## Consequences

### Positive

- Credible AEAD with project isolation via AD + HKDF info
- Schema hook (`FieldDefinition.Encrypt`) can call helpers without EntityEngine knowing NSec

### Negative / follow-ups

- Production must set `MasterKeyBase64` (or later KMS/HSM wrap)
- Key rotation / re-encrypt jobs are deferred
- DataPlane write path must honor `Encrypt` during CRUD (wiring beyond helpers)

## Roadmap placement

| When | Work |
|------|------|
| Phase 10 | Helpers + DI + unit round-trip tests + ADR |
| Later | Automatic encrypt-on-write; key rotation; KMS |
