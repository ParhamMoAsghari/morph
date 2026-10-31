# `@morph/sdk`

Minimal TypeScript client for Morph BaaS (Phase 5).

## Install

```bash
npm install @morph/sdk
# or from this monorepo:
npm install ./sdks/typescript
```

## Usage

```ts
import { MorphClient } from "@morph/sdk";

const morph = new MorphClient({
  baseUrl: "http://localhost:5100",
  environmentId: "<env-guid>",
});

await morph.login("dev@morph.local", "Password1!");
const page = await morph.listRecords("employee");
```

Surfaces: auth, tenancy helpers, definitions, data CRUD, files (create bucket), access list helpers.
