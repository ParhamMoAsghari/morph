import type { WebhookSummary } from "@morph/sdk";
import type { FormEvent } from "react";
import { useEffect, useState } from "react";
import { PageHeader } from "../components/PageHeader";
import { useMorph } from "../lib/MorphContext";

export function WebhooksPage() {
  const { client, session } = useMorph();
  const [items, setItems] = useState<WebhookSummary[]>([]);
  const [url, setUrl] = useState("https://example.com/hooks/morph");
  const [events, setEvents] = useState("entity.created,entity.updated");
  const [error, setError] = useState<string | null>(null);

  async function refresh() {
    if (!client || !session?.projectId) return;
    const list = await client.listWebhooks();
    setItems(Array.isArray(list) ? list : []);
  }

  useEffect(() => {
    void refresh().catch((err) => setError(String(err)));
  }, [client, session?.projectId]);

  async function create(e: FormEvent) {
    e.preventDefault();
    if (!client) return;
    try {
      await client.createWebhook(
        url,
        events.split(",").map((x) => x.trim()).filter(Boolean),
      );
      await refresh();
    } catch (err) {
      setError(String(err));
    }
  }

  return (
    <>
      <PageHeader title="Webhooks" subtitle="HMAC-signed delivery via the Morph outbox." />
      {error ? <div className="error">{error}</div> : null}
      <div className="panel stack">
        <form className="stack" onSubmit={create}>
          <label className="field">
            URL
            <input value={url} onChange={(e) => setUrl(e.target.value)} />
          </label>
          <label className="field">
            Events (comma-separated)
            <input value={events} onChange={(e) => setEvents(e.target.value)} />
          </label>
          <button type="submit">Create webhook</button>
        </form>
        <table>
          <thead>
            <tr>
              <th>URL</th>
              <th>Events</th>
            </tr>
          </thead>
          <tbody>
            {items.map((hook) => (
              <tr key={hook.id}>
                <td className="mono">{hook.url}</td>
                <td className="mono">{(hook.events ?? []).join(", ")}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}
