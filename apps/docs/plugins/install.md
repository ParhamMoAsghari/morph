# Install & load

## Discovery path

Host loads plugins from:

- Config `Plugins:Path` / env `MORPH_Plugins__Path`
- Default: `./plugins` (typically `src/Morph.Host/plugins` when running from that project)

Each plugin is a **folder** with `morph.plugin.json` + assembly. Loading uses a collectible `AssemblyLoadContext` per plugin.

## CLI install

```powershell
dotnet build addons/Morph.Addon.Soap
dotnet run --project tools/Morph.Cli -- plugin install addons/Morph.Addon.Soap/bin/Debug/net9.0 --path src/Morph.Host/plugins
dotnet run --project tools/Morph.Cli -- plugin list --path src/Morph.Host/plugins
```

## Code-first declaration

```yaml
# morph.yaml
plugins:
  - id: morph.soap
    version: "1.0.0"
  - id: morph.mellipayamak
    version: "1.0.0"
    configRef: secrets/mellipayamak
  - id: morph.bale
    version: "1.0.0"
    configRef: secrets/bale
```

## Verify

```http
GET /plugins/v1
```

Returns `{ "plugins": [ … ] }`. Unknown plugin routes return **404**.
