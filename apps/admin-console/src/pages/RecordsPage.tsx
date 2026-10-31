import type { DefinitionSummary, EntityRecord } from "@morph/sdk";
import type { FormEvent } from "react";
import { useEffect, useState } from "react";
import { PageHeader } from "../components/PageHeader";
import { useMorph } from "../lib/MorphContext";

export function RecordsPage() {
  const { client, session } = useMorph();
  const [entities, setEntities] = useState<DefinitionSummary[]>([]);
  const [entity, setEntity] = useState("");
  const [rows, setRows] = useState<EntityRecord[]>([]);
  const [json, setJson] = useState('{"title":"Hello Morph","body":"First post"}');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!client || !session?.environmentId) return;
    void client.listDefinitions().then((defs) => {
      const entitiesOnly = defs.filter((d) => d.kind === "Entity" || !d.kind);
      setEntities(entitiesOnly);
      if (!entity && entitiesOnly[0]) setEntity(entitiesOnly[0].slug);
    });
  }, [client, session?.environmentId]);

  async function load() {
    if (!client || !entity) return;
    setError(null);
    try {
      const page = await client.listRecords(entity, { pageSize: "50" });
      setRows(page.items ?? []);
    } catch (err) {
      setError(String(err));
    }
  }

  useEffect(() => {
    void load();
  }, [entity]);

  async function create(e: FormEvent) {
    e.preventDefault();
    if (!client || !entity) return;
    try {
      await client.createRecord(entity, JSON.parse(json) as Record<string, unknown>);
      await load();
    } catch (err) {
      setError(String(err));
    }
  }

  async function remove(id: string) {
    if (!client || !entity) return;
    await client.deleteRecord(entity, id);
    await load();
  }

  return (
    <>
      <PageHeader title="Records" subtitle="Browse and mutate dynamic entity data." />
      <div className="panel stack">
        <div className="row">
          <label className="field">
            Entity
            <select value={entity} onChange={(e) => setEntity(e.target.value)}>
              {entities.map((d) => (
                <option key={d.definitionId} value={d.slug}>
                  {d.slug}
                </option>
              ))}
            </select>
          </label>
          <button className="secondary" onClick={() => void load()}>
            Refresh
          </button>
        </div>
        {error ? <div className="error">{error}</div> : null}
        <table>
          <thead>
            <tr>
              <th>Id</th>
              <th>Fields</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.id}>
                <td className="mono">{row.id}</td>
                <td className="mono">{JSON.stringify(row.fields ?? row)}</td>
                <td>
                  <button className="danger" onClick={() => void remove(row.id)}>
                    Delete
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="panel">
        <h2>Insert record</h2>
        <form className="stack" onSubmit={create}>
          <textarea value={json} onChange={(e) => setJson(e.target.value)} />
          <button type="submit">Create</button>
        </form>
      </div>
    </>
  );
}
