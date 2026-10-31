# Files API

Prefix: `/files/v1`  
Headers: `Authorization`, `X-Morph-Env-Id`

| Method | Path |
|--------|------|
| POST | `/files/v1/buckets` |
| POST | `/files/v1/buckets/{bucketId}/uploads` |
| POST | `/files/v1/buckets/{bucketId}/uploads/complete` |
| GET | download routes for completed objects |
| DELETE | object delete |

Always **PUT** object bytes before complete — completing without upload fails.
