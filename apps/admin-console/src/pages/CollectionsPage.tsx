import type { DefinitionSummary } from "@morph/sdk";
import type { FormEvent } from "react";
import { useEffect, useState } from "react";
import { JsonBlock } from "../components/JsonBlock";
import { PageHeader } from "../components/PageHeader";
import { useMorph } from "../lib/MorphContext";

export function CollectionsPage() {
  const { client, session } = useMorph();
  const [items, setItems] = useState<DefinitionSummary[]>([]);
  const [selected, setSelected] = useState<DefinitionSummary | null>(null);
  const [effective, setEffective] = useState<unknown>(null);
  const [slug, setSlug] = useState("post");
  const [displayName, setDisplayName] = useState("Post");
  const [fieldsJson, setFieldsJson] = useState(
    '[{"slug":"title","displayName":"Title","dataType":"String","isRequired":true,"isIndexed":true},{"slug":"body","displayName":"Body","dataType":"String"}]',
  );
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function refresh() {
    if (!client || !session?.environmentId) return;
    const list = await client.listDefinitions();
    setItems(list);
  }

  useEffect(() => {
    void refresh().catch((err) => setError(String(err)));
  }, [client, session?.environmentId]);

  async function createCollection(e: FormEvent) {
    e.preventDefault();
    if (!client) return;
    setError(null);
    try {
      const fields = JSON.parse(fieldsJson) as unknown[];
      const created = (await client.createDefinition({
        slug,
        displayName,
        kind: "Entity",
        fields: fields as never,
      })) as { versionId?: string; draftVersionId?: string };
      const versionId = created.versionId ?? created.draftVersionId;
      if (versionId) {
        await client.publishDefinition(versionId);
        await client.planMigration(versionId);
        await client.applyMigration(versionId);
        setMessage(`Published ${slug} and applied storage migration — ready for Records.`);
      }
      await refresh();
    } catch (err) {
      setError(String(err));
    }
  }

  async function showEffective(item: DefinitionSummary) {
    if (!client || !item.currentPublishedVersionId) {
      setEffective({ note: "No published version yet" });
      return;
    }
    setSelected(item);
    setEffective(await client.getEffectiveModel(item.currentPublishedVersionId));
  }

  return (
    <>
      <PageHeader
        title="Collections"
        subtitle="Entity definitions — compose, publish, inspect effective models."
      />
      {!session?.environmentId ? (
        <div className="panel error">Set an environment ID in Settings first.</div>
      ) : null}
      <div className="grid-2">
        <div className="panel">
          <h2>Definitions</h2>
          {error ? <div className="error">{error}</div> : null}
          {message ? <div className="ok">{message}</div> : null}
          <table>
            <thead>
              <tr>
                <th>Slug</th>
                <th>Kind</th>
                <th>Published</th>
              </tr>
            </thead>
            <tbody>
              {items.map((item) => (
                <tr key={item.definitionId}>
                  <td>
                    <button className="list-button" onClick={() => void showEffective(item)}>
                      {item.slug}
                    </button>
                  </td>
                  <td>
                    <span className="badge">{item.kind}</span>
                  </td>
                  <td className="mono">
                    {item.currentPublishedVersionId ? "yes" : "draft"}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="panel stack">
          <h2>Create entity</h2>
          <form className="stack" onSubmit={createCollection}>
            <label className="field">
              Slug
              <input value={slug} onChange={(e) => setSlug(e.target.value)} />
            </label>
            <label className="field">
              Display name
              <input value={displayName} onChange={(e) => setDisplayName(e.target.value)} />
            </label>
            <label className="field">
              Fields JSON
              <textarea value={fieldsJson} onChange={(e) => setFieldsJson(e.target.value)} />
            </label>
            <button type="submit">Create + publish</button>
          </form>
          {selected ? (
            <>
              <h2>Effective model · {selected.slug}</h2>
              <JsonBlock value={effective} />
            </>
          ) : null}
        </div>
      </div>
    </>
  );
}
