import { MorphApiError, MorphClient } from "@morph/sdk";
import type { FormEvent } from "react";
import { useState } from "react";
import { Navigate } from "react-router-dom";
import { useMorph } from "../lib/MorphContext";

export function LoginPage() {
  const { session, connect } = useMorph();
  const [baseUrl, setBaseUrl] = useState("http://localhost:5100");
  const [email, setEmail] = useState("dev@morph.local");
  const [password, setPassword] = useState("Password1!");
  const [projectId, setProjectId] = useState("");
  const [environmentId, setEnvironmentId] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  if (session) {
    return <Navigate to="/" replace />;
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const client = new MorphClient({ baseUrl });
      await client.login(email, password);
      connect({
        baseUrl,
        accessToken: client.getAccessToken()!,
        email,
        projectId: projectId.trim(),
        environmentId: environmentId.trim(),
      });
    } catch (err) {
      setError(err instanceof MorphApiError ? err.message : String(err));
    } finally {
      setBusy(false);
    }
  }

  async function bootstrap() {
    setBusy(true);
    setError(null);
    try {
      const client = new MorphClient({ baseUrl });
      try {
        await client.register(email, password, "Dev");
      } catch {
        /* may already exist */
      }
      await client.login(email, password);
      const org = (await client.createOrganization(
        `Org ${Date.now()}`,
        `org-${Date.now()}`,
      )) as { id?: string; organizationId?: string };
      const orgId = org.id ?? org.organizationId;
      if (!orgId) throw new Error("Organization create did not return id");
      const project = (await client.createProject(
        orgId,
        "Demo Project",
        `proj-${Date.now()}`,
      )) as { id?: string; projectId?: string };
      const pid = project.id ?? project.projectId;
      if (!pid) throw new Error("Project create did not return id");
      const env = (await client.createEnvironment(pid, "Development", "dev")) as {
        id?: string;
        environmentId?: string;
      };
      const eid = env.id ?? env.environmentId;
      if (!eid) throw new Error("Environment create did not return id");
      client.setProjectId(pid);
      client.setEnvironmentId(eid);
      // Seed built-in roles then grant yourself owner so Records CRUD works.
      await client.listRoles(pid);
      const me = (await client.me()) as { id?: string };
      if (me.id) {
        await client.bindRole(me.id, "owner", eid, pid);
      }
      setProjectId(pid);
      setEnvironmentId(eid);
      connect({
        baseUrl,
        accessToken: client.getAccessToken()!,
        email,
        projectId: pid,
        environmentId: eid,
      });
    } catch (err) {
      setError(err instanceof MorphApiError ? err.message : String(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="login-screen">
      <form className="login-card stack" onSubmit={onSubmit}>
        <div>
          <h1>Morph</h1>
          <p className="lede">
            Developer panel for entities, access, storage, processes, functions, and more.
          </p>
        </div>
        <label className="field">
          API base URL
          <input value={baseUrl} onChange={(e) => setBaseUrl(e.target.value)} />
        </label>
        <label className="field">
          Email
          <input value={email} onChange={(e) => setEmail(e.target.value)} />
        </label>
        <label className="field">
          Password
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
        </label>
        <label className="field">
          Project ID
          <input
            value={projectId}
            onChange={(e) => setProjectId(e.target.value)}
            placeholder="optional if bootstrapping"
          />
        </label>
        <label className="field">
          Environment ID
          <input
            value={environmentId}
            onChange={(e) => setEnvironmentId(e.target.value)}
            placeholder="optional if bootstrapping"
          />
        </label>
        {error ? <div className="error">{error}</div> : null}
        <div className="row">
          <button type="submit" disabled={busy}>
            Sign in
          </button>
          <button type="button" className="secondary" disabled={busy} onClick={bootstrap}>
            Register + bootstrap project
          </button>
        </div>
      </form>
    </div>
  );
}
