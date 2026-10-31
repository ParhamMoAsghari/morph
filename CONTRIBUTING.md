# Contributing to Morph

Thanks for your interest in Morph. This document is the collaboration policy for
the project: how we work together, what we accept, and how to ship a change
safely.

By participating, you agree to follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Ways to contribute

- **Bugs** — open an issue with reproduction steps, expected vs actual behavior,
  Host/SDK versions, and logs (redact secrets).
- **Features** — open an issue first for non-trivial ideas so we can align on
  design before code.
- **Docs** — fixes and clarity improvements in `apps/docs` and `docs/` are always
  welcome.
- **Tests** — regression tests that lock intended behavior (see
  [docs/test-based-functional-validity.md](docs/test-based-functional-validity.md)).
- **Plugins / addons** — reference addons live under `addons/`; follow
  `apps/docs/plugins/` for the contract.

## Development setup

Prerequisites: .NET 9 SDK, Node.js 18+, Docker Desktop.

```powershell
cd deploy
docker compose up -d postgres redis mailpit

cd ..
dotnet build Morph.sln
dotnet run --project src/Morph.Host --launch-profile http

# Docs (optional)
cd apps/docs
npm install
npm run dev
```

More detail: [README.md](README.md) and the [Quickstart](apps/docs/guide/quickstart.md).

## Branching and pull requests

1. Fork the repository (or use a branch if you have write access).
2. Create a topic branch from `main`: `git checkout -b feat/short-name`.
3. Keep PRs focused — one concern per PR when practical.
4. Fill in the PR template: **what**, **why**, **how tested**.
5. Ensure CI is green (`dotnet build`, `dotnet test`, docs build if you touched docs).
6. Request review; address feedback with follow-up commits (or squash as agreed).

### Commit messages

Prefer short, imperative subjects:

- `fix access evaluator null principal handling`
- `docs: clarify public.read policy publish step`
- `feat(cli): recreate missing in-memory policies on push`

## Coding guidelines

- Match existing style in the area you touch; do not drive-by reformat unrelated files.
- Prefer small, reviewable diffs.
- For Access, DataPlane, Identity, and plugins: add or update tests that would
  fail without your change.
- Do not commit secrets, real connection strings, or customer data.
- Do not add new dependencies without a clear need and license compatibility
  with **Apache-2.0**.

## Review criteria

Maintainers look for:

1. Correctness and security (especially authz, tenancy isolation, file access).
2. Tests or documented reason why tests are not applicable.
3. Docs / changelog notes when public behavior or APIs change.
4. No unnecessary breaking changes; if breaking, call it out explicitly.

## Release and versioning posture

Morph is evolving quickly. Public HTTP/SDK surfaces should stay backward
compatible within a minor line when possible. Breaking changes should be
discussed in an issue/ADR under `docs/adr/` before merge when they affect
external consumers.

## Security reports

Do **not** open a public issue for vulnerabilities. See [SECURITY.md](SECURITY.md).

## License

Contributions are licensed under the [Apache License 2.0](LICENSE). Unless you
state otherwise, opening a PR means you agree your contribution is offered under
that license (DCO-style: you have the right to submit it).

## Getting help

- Prefer GitHub Discussions / Issues for design questions.
- Keep conversation technical and respectful; escalate CoC issues privately as
  described in [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).
