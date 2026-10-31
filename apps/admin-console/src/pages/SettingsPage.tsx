import type { FormEvent } from "react";
import { useState } from "react";
import { PageHeader } from "../components/PageHeader";
import { useMorph } from "../lib/MorphContext";

export function SettingsPage() {
  const { session, updateScope, client } = useMorph();
  const [projectId, setProjectId] = useState(session?.projectId ?? "");
  const [environmentId, setEnvironmentId] = useState(session?.environmentId ?? "");
  const [smsTo, setSmsTo] = useState("");
  const [smsBody, setSmsBody] = useState("Morph test");
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  function saveScope(e: FormEvent) {
    e.preventDefault();
    updateScope(projectId.trim(), environmentId.trim());
    setMessage("Scope saved.");
  }

  async function sendSms(e: FormEvent) {
    e.preventDefault();
    if (!client) return;
    setError(null);
    try {
      await client.sendSms(smsTo, smsBody);
      setMessage("SMS send requested.");
    } catch (err) {
      setError(String(err));
    }
  }

  return (
    <>
      <PageHeader title="Settings" subtitle="Project/environment scope and utilities." />
      <div className="grid-2">
        <div className="panel">
          <h2>Active scope</h2>
          <form className="stack" onSubmit={saveScope}>
            <label className="field">
              Project ID
              <input value={projectId} onChange={(e) => setProjectId(e.target.value)} />
            </label>
            <label className="field">
              Environment ID
              <input value={environmentId} onChange={(e) => setEnvironmentId(e.target.value)} />
            </label>
            <button type="submit">Save</button>
          </form>
          {message ? <div className="ok">{message}</div> : null}
        </div>
        <div className="panel">
          <h2>SMS (Morph.Sms / plugins)</h2>
          <form className="stack" onSubmit={sendSms}>
            <label className="field">
              To
              <input value={smsTo} onChange={(e) => setSmsTo(e.target.value)} />
            </label>
            <label className="field">
              Message
              <input value={smsBody} onChange={(e) => setSmsBody(e.target.value)} />
            </label>
            <button type="submit">Send</button>
          </form>
          {error ? <div className="error">{error}</div> : null}
        </div>
      </div>
    </>
  );
}
