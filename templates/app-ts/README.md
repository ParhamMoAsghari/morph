# Morph TypeScript starter (code-first)

Shows the git → CLI → SDK path **without** the admin console. See [docs/code-first.md](../../docs/code-first.md).

```powershell
# From repo root after Host is running:
dotnet run --project tools/Morph.Cli -- init   # or copy this template
dotnet run --project tools/Morph.Cli -- login --email … --password …
dotnet run --project tools/Morph.Cli -- link --project … --env …
dotnet run --project tools/Morph.Cli -- push
dotnet run --project tools/Morph.Cli -- publish
dotnet run --project tools/Morph.Cli -- migrate --apply

cd templates/app-ts
npm install
$env:MORPH_ENV_ID="<env-guid>"
npm start
```
