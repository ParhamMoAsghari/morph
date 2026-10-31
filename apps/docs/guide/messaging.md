# SMS & email

## SMS

```http
POST /v1/sms/send
{
  "to": "+15555550100",
  "body": "Your Morph blog is live."
}
```

- Missing `to` / body → **400**
- Without a real provider plugin, Development/tests use a **test/null sender** that still returns `succeeded: true` with a test message id
- Install **Mellipayamak** addon for a real Iranian SMS provider facade

SDK:

```ts
await morph.sendSms("+15555550100", "Hello");
```

## Email

SMTP via Morph.Mail (Mailpit in Compose: http://localhost:8025). Templates and productized Morph Mail are evolving; Host wiring uses Development SMTP settings from config / `MORPH_*` env vars.
