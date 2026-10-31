import type { ProcessSummary } from "@morph/sdk";
import { useEffect, useState } from "react";
import { JsonBlock } from "../components/JsonBlock";
import { PageHeader } from "../components/PageHeader";
import { useMorph } from "../lib/MorphContext";

export function ProcessesPage() {
  const { client, session } = useMorph();
  const [items, setItems] = useState<ProcessSummary[]>([]);
  const [slug, setSlug] = useState("");
  const [input, setInput] = useState("{}");
  const [result, setResult] = useState<unknown>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!client || !session?.environmentId) return;
    void client
      .listProcesses()
      .then((list) => {
        const rows = Array.isArray(list) ? list : [];
        setItems(rows);
        if (rows[0]?.slug) setSlug(rows[0].slug);
      })
      .catch((err) => setError(String(err)));
  }, [client, session?.environmentId]);

  async function run() {
    if (!client || !slug) return;
    setError(null);
    try {
      setResult(await client.runProcess(slug, JSON.parse(input) as Record<string, unknown>));
    } catch (err) {
      setError(String(err));
    }
  }

  return (
    <>
      <PageHeader
        title="Processes"
        subtitle="Low-code / no-code Process Engine runs (semi-BPMS)."
      />
      <div className="grid-2">
        <div className="panel">
          <h2>Definitions</h2>
          <table>
            <thead>
              <tr>
                <th>Slug</th>
                <th>Published</th>
              </tr>
            </thead>
            <tbody>
              {items.map((p) => (
                <tr key={p.definitionId}>
                  <td>
                    <button className="list-button" onClick={() => setSlug(p.slug)}>
                      {p.slug}
                    </button>
                  </td>
                  <td className="mono">{p.currentPublishedVersionId ? "yes" : "no"}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <p style={{ color: "var(--muted)", fontSize: "0.85rem" }}>
            Push process YAML with <span className="mono">morph process push</span>.
          </p>
        </div>
        <div className="panel stack">
          <h2>Run</h2>
          <label className="field">
            Slug
            <input value={slug} onChange={(e) => setSlug(e.target.value)} />
          </label>
          <label className="field">
            Input JSON
            <textarea value={input} onChange={(e) => setInput(e.target.value)} />
          </label>
          <button onClick={() => void run()}>Start instance</button>
          {error ? <div className="error">{error}</div> : null}
          {result ? <JsonBlock value={result} /> : null}
        </div>
      </div>
    </>
  );
}
