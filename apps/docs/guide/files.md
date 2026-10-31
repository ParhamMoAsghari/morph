# Files & storage

Object storage is MinIO/S3-compatible (or in-memory for tests). Host can boot without MinIO; set `Morph:ObjectStorage:Endpoint` when the storage profile is up.

## Flow

1. Create a **bucket**
2. Create an **upload** (presigned / staged)
3. **PUT** bytes to the upload URL
4. **Complete** the upload → file becomes Ready
5. Download / delete as needed

```http
POST /files/v1/buckets
X-Morph-Env-Id: …
{ "slug": "media", "displayName": "Blog Media" }

POST /files/v1/buckets/{bucketId}/uploads
{ "fileName": "hero.png", "contentType": "image/png" }

# PUT object bytes to returned URL

POST /files/v1/buckets/{bucketId}/uploads/complete
{ "uploadId": "…" }
```

Disallowed content types are rejected (Host e2e).

## SDK / Panel

```ts
await morph.createBucket("media", "Blog Media");
```

The Dev Panel Storage page creates buckets; full upload UX can be driven via API/SDK.
