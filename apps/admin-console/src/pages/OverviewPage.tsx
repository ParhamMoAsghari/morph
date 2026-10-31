import { useEffect, useState } from "react";
import { JsonBlock } from "../components/JsonBlock";
import { PageHeader } from "../components/PageHeader";
import { useMorph } from "../lib/MorphContext";

export function OverviewPage() {
  const { client, session } = useMorph();
  const [health, setHealth] = useState<unknown>(null);
  const [defs, setDefs] = useState(0);
  const [plugins, setPlugins] = useState(0);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!client) return;
    void (async () => {
      try {
        const [h, d, p] = await Promise.all([
          client.healthReady(),
          session?.environmentId ? client.listDefinitions() : Promise.resolve([]),
          client.listPlugins().catch(() => []),
        ]);
        setHealth(h);
        setDefs(Array.isArray(d) ? d.length : 0);
        setPlugins(Array.isArray(p) ? p.length : 0);
      } catch (err) {
        setError(String(err));
      }
    })();
  }, [client, session?.environmentId]);

  return (
    <>
      <PageHeader
        title="Overview"
        subtitle="PocketBase/Supabase-style control surface for every Morph capability."
      />
      <div className="grid-3">
        <div className="stat">
          <strong>{defs}</strong>
          <span>Entity definitions</span>
        </div>
        <div className="stat">
          <strong>{plugins}</strong>
          <span>Loaded plugins</span>
        </div>
        <div className="stat">
          <strong className="mono" style={{ fontSize: "0.95rem" }}>
            {session?.environmentId?.slice(0, 8) || "—"}
          </strong>
          <span>Active environment</span>
        </div>
      </div>
      <div className="panel" style={{ marginTop: "1rem" }}>
        <h2>Host health</h2>
        {error ? <div className="error">{error}</div> : null}
        {health ? <JsonBlock value={health} /> : <p className="ok">Loading…</p>}
      </div>
    </>
  );
}
