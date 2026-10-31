import type { FormEvent } from "react";
import { useState } from "react";
import { JsonBlock } from "../components/JsonBlock";
import { PageHeader } from "../components/PageHeader";
import { useMorph } from "../lib/MorphContext";

export function StoragePage() {
  const { client } = useMorph();
  const [slug, setSlug] = useState("media");
  const [result, setResult] = useState<unknown>(null);
  const [error, setError] = useState<string | null>(null);

  async function createBucket(e: FormEvent) {
    e.preventDefault();
    if (!client) return;
    setError(null);
    try {
      setResult(await client.createBucket(slug, slug));
    } catch (err) {
      setError(String(err));
    }
  }

  return (
    <>
      <PageHeader
        title="Storage"
        subtitle="Buckets and Access-gated signed uploads/downloads (MinIO/S3)."
      />
      <div className="panel stack">
        <form className="row" onSubmit={createBucket}>
          <label className="field">
            Bucket slug
            <input value={slug} onChange={(e) => setSlug(e.target.value)} />
          </label>
          <button type="submit">Create bucket</button>
        </form>
        {error ? <div className="error">{error}</div> : null}
        {result ? <JsonBlock value={result} /> : null}
        <p style={{ color: "var(--muted)", fontSize: "0.85rem" }}>
          Presigned upload/download flows use <span className="mono">/files/v1</span>. Ensure
          Compose <span className="mono">--profile storage</span> is running for MinIO.
        </p>
      </div>
    </>
  );
}
