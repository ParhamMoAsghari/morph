import type { FormEvent } from "react";
import { useEffect, useState } from "react";
import { JsonBlock } from "../components/JsonBlock";
import { PageHeader } from "../components/PageHeader";
import { useMorph } from "../lib/MorphContext";

export function ApiPage() {
  const { client, session } = useMorph();
  const [credentials, setCredentials] = useState<unknown[]>([]);
  const [issued, setIssued] = useState<unknown>(null);
  const [gql, setGql] = useState(
    'query { __typename }',
  );
  const [sql, setSql] = useState("SELECT 1");
  const [result, setResult] = useState<unknown>(null);
  const [error, setError] = useState<string | null>(null);

  async function refreshCreds() {
    if (!client || !session?.projectId) return;
    const list = await client.listCredentials();
    setCredentials(Array.isArray(list) ? list : []);
  }

  useEffect(() => {
    void refreshCreds().catch((err) => setError(String(err)));
  }, [client, session?.projectId]);

  async function issue(e: FormEvent) {
    e.preventDefault();
    if (!client) return;
    try {
      setIssued(await client.issueCredential(`key-${Date.now()}`));
      await refreshCreds();
    } catch (err) {
      setError(String(err));
    }
  }

  async function runGraphql(e: FormEvent) {
    e.preventDefault();
    if (!client) return;
    try {
      setResult(await client.graphql(gql));
    } catch (err) {
      setError(String(err));
    }
  }

  async function runSql(e: FormEvent) {
    e.preventDefault();
    if (!client) return;
    try {
      setResult(await client.sql(sql));
    } catch (err) {
      setError(String(err));
    }
  }

  return (
    <>
      <PageHeader
        title="API & SQL"
        subtitle="Credentials, GraphQL (primary), and governed SQL."
      />
      {error ? <div className="error">{error}</div> : null}
      <div className="grid-2">
        <div className="panel stack">
          <h2>API credentials</h2>
          <form onSubmit={issue}>
            <button type="submit">Issue key</button>
          </form>
          {issued ? <JsonBlock value={issued} /> : null}
          <JsonBlock value={credentials} />
        </div>
        <div className="panel stack">
          <h2>GraphQL</h2>
          <form className="stack" onSubmit={runGraphql}>
            <textarea value={gql} onChange={(e) => setGql(e.target.value)} />
            <button type="submit">Execute</button>
          </form>
          <h2>Governed SQL</h2>
          <form className="stack" onSubmit={runSql}>
            <textarea value={sql} onChange={(e) => setSql(e.target.value)} />
            <button type="submit">Execute</button>
          </form>
          {result ? <JsonBlock value={result} /> : null}
        </div>
      </div>
    </>
  );
}
