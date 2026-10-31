# Storage & crypto

## Object storage

`Morph.Files` + `Morph.ObjectStore` abstract S3-compatible storage. Development Compose profile starts MinIO; tests may use in-memory implementations.

## StorageEngine facade

`Morph.StorageEngine` documents multi-engine / Citus-oriented facades — see [Multi-region](/architecture/multi-region).

## Crypto

`Morph.Crypto` provides project-scoped encryption helpers (ciphertext differs across projects). Covered by dedicated unit tests and ADR-026.
