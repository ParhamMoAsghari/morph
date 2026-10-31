import type { FunctionSummary } from "@morph/sdk";
import type { FormEvent } from "react";
import { useEffect, useState } from "react";
import { JsonBlock } from "../components/JsonBlock";
import { PageHeader } from "../components/PageHeader";
import { useMorph } from "../lib/MorphContext";

export function FunctionsPage() {
  const { client, session } = useMorph();
  const [items, setItems] = useState<FunctionSummary[]>([]);
  const [slug, setSlug] = useState("hello");
  const [runtime, setRuntime] = useState("typescript");
  const [payload, setPayload] = useState("{}");
  const [result, setResult] = useState<unknown>(null);
  const [error, setError] = useState<string | null>(null);

  async function refresh() {
    if (!client || !session?.environmentId) return;
    const list = await client.listFunctions();
    setItems(Array.isArray(list) ? list : []);
  }

  useEffect(() => {
    void refresh().catch((err) => setError(String(err)));
  }, [client, session?.environmentId]);

  async function register(e: FormEvent) {
    e.preventDefault();
    if (!client) return;
    try {
      await client.registerFunction(slug, runtime, "// stub");
      await refresh();
    } catch (err) {
      setError(String(err));
    }
  }

  async function invoke() {
    if (!client) return;
    try {
      setResult(
        await client.invokeFunction(slug, JSON.parse(payload) as Record<string, unknown>),
      );
    } catch (err) {
      setError(String(err));
    }
  }

  return (
    <>
      <PageHeader title="Functions" subtitle="Deployable code hooks invocable from processes." />
      {error ? <div className="error">{error}</div> : null}
      <div className="grid-2">
        <div className="panel">
          <h2>Registered</h2>
          <table>
            <thead>
              <tr>
                <th>Slug</th>
                <th>Runtime</th>
              </tr>
            </thead>
            <tbody>
              {items.map((fn) => (
                <tr key={fn.slug}>
                  <td className="mono">{fn.slug}</td>
                  <td>{fn.runtime}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="panel stack">
          <form className="stack" onSubmit={register}>
            <label className="field">
              Slug
              <input value={slug} onChange={(e) => setSlug(e.target.value)} />
            </label>
            <label className="field">
              Runtime
              <select value={runtime} onChange={(e) => setRuntime(e.target.value)}>
                <option value="typescript">typescript</option>
                <option value="dotnet">dotnet</option>
              </select>
            </label>
            <button type="submit">Register stub</button>
          </form>
          <label className="field">
            Invoke payload
            <textarea value={payload} onChange={(e) => setPayload(e.target.value)} />
          </label>
          <button className="secondary" onClick={() => void invoke()}>
            Invoke
          </button>
          {result ? <JsonBlock value={result} /> : null}
        </div>
      </div>
    </>
  );
}
