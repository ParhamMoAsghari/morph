# Versioning posture

- Host reports a **phase** integer on `GET /` (composition maturity marker).
- SDKs use semver in package metadata (`@morph/sdk`, `Morph.Client`).
- Plugin manifests carry their own `version` string.
- Definition **versions** are immutable once published; migrations move storage forward.
- Docs site version tracks the monorepo; regenerate coverage snapshots when validity numbers change.

Breaking HTTP changes should ship with SDK updates and a note here / in ADRs.
