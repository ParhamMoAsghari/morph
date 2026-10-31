# Webhooks

Outbound subscriptions notify your HTTPS endpoints of Morph events.

## Create

```http
POST /v1/projects/{projectId}/webhooks
{
  "targetUrl": "https://example.com/hooks/morph",
  "environmentId": "<env-id>",
  "eventTypes": ["record.created", "record.updated"]
}
```

## Rules

- **Public HTTPS** targets — private/loopback URLs are rejected (SSRF protection).
- Deliveries are signed (HMAC `X-Morph-Signature` — see Messaging tests).
- List / delete under the same project path.

## SDK

```ts
await morph.createWebhook("https://example.com/hooks/morph", [
  "record.created",
  "record.updated",
]);
```

SDK maps `url`/`events` ↔ API `targetUrl`/`eventTypes`.
