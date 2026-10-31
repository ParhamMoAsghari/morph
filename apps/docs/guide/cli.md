# CLI & code-first

`tools/Morph.Cli` — the `morph` CLI — treats **git as source of truth**.

## Layout

```text
my-app/
  morph.yaml
  morph/
    definitions/
    policies/
    processes/
  src/
```

## Workflow

```powershell
dotnet run --project tools/Morph.Cli -- init --name my-app
dotnet run --project tools/Morph.Cli -- login --email dev@morph.local --password 'Password1!'
dotnet run --project tools/Morph.Cli -- link --project <guid> --env <guid>

dotnet run --project tools/Morph.Cli -- push
dotnet run --project tools/Morph.Cli -- diff
dotnet run --project tools/Morph.Cli -- publish
dotnet run --project tools/Morph.Cli -- migrate --apply
dotnet run --project tools/Morph.Cli -- generate --lang all
```

## Plugins

```powershell
dotnet run --project tools/Morph.Cli -- plugin list
dotnet run --project tools/Morph.Cli -- plugin install <built-addon-dir> --path src/Morph.Host/plugins
```

`morph.yaml` can declare:

```yaml
plugins:
  - id: morph.soap
    version: "1.0.0"
  - id: morph.bale
    version: "1.0.0"
    configRef: secrets/bale
```

## Notes

- Field/definition UUIDs in YAML are stable — keep them in git
- `morph pull` writes remote published defs to disk
- Definition **updates** may require draft APIs; push creates missing slugs
- See also repo `docs/code-first.md`
