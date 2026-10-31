# Security Policy

## Supported versions

Morph is under active early development. Security fixes are applied on the
default branch (`main`). If you are running a fork or older snapshot, rebase onto
`main` when consuming fixes.

## Reporting a vulnerability

Please **do not** file a public GitHub issue for security problems.

Preferred options (in order):

1. **GitHub Security Advisories** — use *Report a vulnerability* on this
   repository (Private vulnerability reporting).
2. Email maintainers at **security@morph.dev** (if published) or contact a
   repository owner privately via GitHub.

Include:

- Affected component (Host, Access, DataPlane, Files, SDK, CLI, addon, …)
- Morph / commit SHA if known
- Steps to reproduce (PoC welcome; keep it minimal)
- Impact assessment (auth bypass, cross-tenant read, RCE, DoS, …)

We will acknowledge receipt as soon as practical, assess severity, and
coordinate disclosure. Please give us a reasonable window before public
discussion.

## Safe harbor

We welcome good-faith research. Do not access other users’ data, disrupt
production systems you do not own, or exfiltrate data beyond what is needed to
demonstrate the issue.

## Secrets in this repo

Never commit real credentials. Use `.env.example` and local overrides ignored by
git. Rotate anything that may have been exposed.
