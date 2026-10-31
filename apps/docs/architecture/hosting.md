# Hosting & configuration

## Run

```powershell
dotnet run --project src/Morph.Host --launch-profile http
```

Default HTTP: **5100**.

## Configuration

- `appsettings.json` / `appsettings.Development.json`
- Environment variables with prefix **`MORPH_`** (nested via `__`)
- Compose `.env` from `.env.example`

Important knobs:

| Key | Purpose |
|-----|---------|
| `ConnectionStrings:Platform` | Postgres |
| `Redis:ConnectionString` | Redis |
| `ObjectStorage:*` | MinIO/S3 |
| `Mail:*` | SMTP |
| `Auth:Issuer` / platform client secret | OpenIddict |
| `Plugins:Path` | Addon folder |

## CORS

Development allows Dev Panel origins on `5173` / `4173` (localhost and 127.0.0.1).

## Observability

Serilog request logging, OpenTelemetry registration, health checks, Hangfire dashboard under `/internal/hangfire`.
