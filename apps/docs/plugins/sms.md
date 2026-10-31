# SMS providers

```csharp
builder.AddSmsProvider<MellipayamakSmsProvider>();
```

Requires `sms.send`.

Implement `ISmsProvider` to send through a carrier API. The Host’s Morph SMS facade selects configured providers; without a plugin, Development uses null/test senders so `/v1/sms/send` still validates and returns a structured result.

See `addons/Morph.Addon.Mellipayamak`.
