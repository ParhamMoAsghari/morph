# Functions

Register invocable code hooks (stub runtimes today).

## Register

```http
POST /v1/environments/{envId}/functions
{
  "slug": "echo",
  "displayName": "Echo",
  "runtime": "TypeScriptStub",
  "trigger": "Http",
  "source": "export default async (ctx) => ctx.input"
}
```

Runtimes: TypeScript stub / DotNet stub. Triggers: Http, Lifecycle, Process.

## Invoke

```http
POST /v1/environments/{envId}/functions/echo/invoke
{ "input": { "ping": true } }
```

Response includes `succeeded`, `output`, `error`, `durationMs`.

## SDK

```ts
await morph.registerFunction("echo", "typescript", "export default async (ctx) => ctx.input");
await morph.invokeFunction("echo", { ping: true });
```

## From processes

Use activity type `morph.functions.invoke` to call a function mid-workflow.

## Limits

Stub invokers prove orchestration. Hardened isolate sandboxes and packaging are roadmap items ([ADR-025](/reference/adrs)).
