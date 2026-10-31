# .NET — Morph.Client

Project: `sdks/dotnet/Morph.Client`

```csharp
using Morph.Client;

using var client = new MorphClient(new MorphClientOptions
{
    BaseAddress = new Uri("http://localhost:5100/"),
    EnvironmentId = envId
});

await client.Auth.LoginAsync("dev@morph.local", "Password1!");
await client.Data.CreateAsync("employee", new { employeeNumber = "E-1" });
```

Clients are split by area (`Auth`, `Data`, tenancy/definitions helpers — see `Morph.Client.Tests` for HTTP contract stubs).

Add a project reference or pack the SDK for your worker/API. Prefer the same environment id and JWT lifecycle as the TypeScript client.
