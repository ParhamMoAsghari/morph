# Reference addons

Shipped under `/addons` as teaching implementations and useful connectors.

| Project | Id | Highlights |
|---------|-----|------------|
| `Morph.Addon.Soap` | `morph.soap` | Process activity `plugin:morph.soap.invoke` |
| `Morph.Addon.Mellipayamak` | `morph.mellipayamak` | `ISmsProvider` + send activity stub |
| `Morph.Addon.Bale` | `morph.bale` | Inbound webhook + send/callback activities |

Build and install:

```powershell
dotnet build addons/Morph.Addon.Bale
dotnet run --project tools/Morph.Cli -- plugin install addons/Morph.Addon.Bale/bin/Debug/net9.0 --path src/Morph.Host/plugins
```

Read their `morph.plugin.json` and `*Plugin.cs` before inventing new permission combinations.
