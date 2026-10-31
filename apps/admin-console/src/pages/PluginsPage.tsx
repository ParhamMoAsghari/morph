import type { PluginSummary } from "@morph/sdk";
import { useEffect, useState } from "react";
import { PageHeader } from "../components/PageHeader";
import { useMorph } from "../lib/MorphContext";

export function PluginsPage() {
  const { client } = useMorph();
  const [items, setItems] = useState<PluginSummary[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!client) return;
    void client
      .listPlugins()
      .then((list) => setItems(Array.isArray(list) ? list : []))
      .catch((err) => setError(String(err)));
  }, [client]);

  return (
    <>
      <PageHeader
        title="Plugins"
        subtitle="Installed addons (Bale, Mellipayamak, SOAP, custom)."
      />
      <div className="panel">
        {error ? <div className="error">{error}</div> : null}
        <table>
          <thead>
            <tr>
              <th>Id</th>
              <th>Version</th>
              <th>Name</th>
            </tr>
          </thead>
          <tbody>
            {items.map((plugin) => (
              <tr key={plugin.id}>
                <td className="mono">{plugin.id}</td>
                <td>{plugin.version}</td>
                <td>{plugin.displayName ?? plugin.id}</td>
              </tr>
            ))}
          </tbody>
        </table>
        <p style={{ color: "var(--muted)", fontSize: "0.85rem" }}>
          Install with <span className="mono">morph plugin install …</span> into the Host plugins
          path.
        </p>
      </div>
    </>
  );
}
