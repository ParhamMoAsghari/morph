# Auth API

| Method | Path | Description |
|--------|------|-------------|
| POST | `/v1/auth/register` | Create user |
| POST | `/connect/token` | OpenIddict token endpoint |
| GET | `/v1/auth/me` | Current user |
| POST | `/v1/auth/forgot-password` | Start reset (no enumeration) |
| POST | `/v1/auth/reset-password` | Complete reset |
| POST | `/v1/auth/verify-email` | Verify email token |

Token request is `application/x-www-form-urlencoded` (OAuth password grant for the platform client).
