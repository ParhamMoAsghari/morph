# Testing & functional validity

Morph’s binding quality gate is **test-based functional validity**: ideal HTTP/SDK contracts, not coverage theater.

Full document in the repo: [`docs/test-based-functional-validity.md`](../../../docs/test-based-functional-validity.md) — mirrored summary below. Prefer that file for the latest measured numbers.

## Snapshot (last measured in-repo)

| Metric | Value |
|--------|------:|
| .NET tests | ~289 passed |
| Line coverage (merged) | ~77.9% |
| Branch coverage | ~45.7% |
| Method coverage | ~85.5% |

## Panel proof

`CommunityBlogPanelWorkflowTests` builds a real Community Blog through every Dev Panel API surface.

```powershell
dotnet test tests/Morph.Host.Integration.Tests --filter CommunityBlogPanelWorkflowTests
dotnet test Morph.sln
```

## Coverage regenerate

```powershell
dotnet test Morph.sln --collect:"XPlat Code Coverage" --results-directory ./TestResults
reportgenerator "-reports:TestResults/**/coverage.cobertura.xml" "-targetdir:TestResults/merged-coverage" "-reporttypes:TextSummary;HtmlSummary"
```

## Release gates

1. `dotnet test Morph.sln` → 0 failed  
2. Host integration → 0 failed (Docker)  
3. Community Blog e2e → pass  
4. Line coverage floor ≥ 75% without rubber-stamp tests  
5. New Host endpoints have e2e or an explicit Gap row  

See the full capability matrix (Proven / Partial / Gap) in `docs/test-based-functional-validity.md`.
