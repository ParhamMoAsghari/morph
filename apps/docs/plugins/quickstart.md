# Quickstart: your first addon

## 1. Create a class library

```powershell
dotnet new classlib -n Acme.Morph.Hello -o addons/Acme.Morph.Hello -f net9.0
dotnet add addons/Acme.Morph.Hello/Acme.Morph.Hello.csproj reference src/Morph.Plugins.Abstractions/Morph.Plugins.Abstractions.csproj
```

## 2. Implement the plugin

```csharp
using Morph.Plugins.Abstractions;

namespace Acme.Morph.Hello;

public sealed class HelloPlugin : IMorphPlugin
{
    public PluginManifest Manifest { get; } = new()
    {
        Id = "acme.hello",
        Version = "1.0.0",
        DisplayName = "Hello",
        Permissions = [PluginPermissions.HttpInbound]
    };

    public Task RegisterAsync(IPluginBuilder builder, CancellationToken ct)
    {
        builder.AddHttpRoute("ping", async (ctx, token) =>
        {
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("""{"ok":true,"plugin":"acme.hello"}""", token);
        });
        return Task.CompletedTask;
    }
}
```

## 3. Manifest file

Create `morph.plugin.json` next to the built DLL (or copy on build):

```json
{
  "id": "acme.hello",
  "version": "1.0.0",
  "displayName": "Hello",
  "entryType": "Acme.Morph.Hello.HelloPlugin",
  "assembly": "Acme.Morph.Hello.dll",
  "permissions": ["http.inbound"],
  "capabilities": ["http.inbound"]
}
```

Ensure `entryType` matches the fully qualified type name and permissions match what `RegisterAsync` calls.

## 4. Install into Host

```powershell
dotnet build addons/Acme.Morph.Hello
dotnet run --project tools/Morph.Cli -- plugin install addons/Acme.Morph.Hello/bin/Debug/net9.0 --path src/Morph.Host/plugins
dotnet run --project src/Morph.Host --launch-profile http
```

## 5. Call it

```http
GET http://localhost:5100/plugins/v1/acme.hello/ping
```

Catalog:

```http
GET http://localhost:5100/plugins/v1
```

## Next

Add a [process activity](/plugins/activities) or [SMS provider](/plugins/sms) with the matching permission.
