# Process activities

Plugins contribute steps the Process Engine can execute.

## Register

```csharp
builder.AddProcessActivity<MyActivity>("plugin:morph.example.doWork");
```

Requires `process.activities`.

## Implement

Implement `IProcessActivity` from Abstractions (see Soap / Bale / Mellipayamak addons for patterns). Activities receive execution context from the process runner and should be deterministic where possible; use outbound HTTP factory for external calls.

## Authoring

In process YAML:

```yaml
steps:
  - id: call-soap
    type: plugin:morph.soap.invoke
    with:
      # activity-specific inputs
```

Exact `with` schema is defined by your activity.

## Reference

- `Morph.Addon.Soap` → `plugin:morph.soap.invoke`
- `Morph.Addon.Bale` → send / callback activities
- `Morph.Addon.Mellipayamak` → SMS send activity stub
