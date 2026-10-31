# Inbound HTTP & webhooks

Requires `http.inbound`.

## Simple route

```csharp
builder.AddHttpRoute("updates", async (ctx, ct) => { /* … */ });
```

Becomes: `/plugins/v1/{pluginId}/updates`

## Webhook handler

```csharp
builder.AddInboundWebhook("hooks", new MyWebhookHandler());
```

Implement `IInboundWebhookHandler` for structured webhook processing (signature checks, enqueue process, etc.).

## Example

Bale: `POST /plugins/v1/morph.bale/updates`
