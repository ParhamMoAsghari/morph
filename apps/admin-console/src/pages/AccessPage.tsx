import type { PolicySummary, RoleSummary } from "@morph/sdk";
import type { FormEvent } from "react";
import { useEffect, useState } from "react";
import { PageHeader } from "../components/PageHeader";
import { useMorph } from "../lib/MorphContext";

export function AccessPage() {
  const { client, session } = useMorph();
  const [roles, setRoles] = useState<RoleSummary[]>([]);
  const [policies, setPolicies] = useState<PolicySummary[]>([]);
  const [name, setName] = useState("editor");
  const [slug, setSlug] = useState("editor");
  const [error, setError] = useState<string | null>(null);

  async function refresh() {
    if (!client || !session?.projectId) return;
    const [r, p] = await Promise.all([client.listRoles(), client.listPolicies()]);
    setRoles(Array.isArray(r) ? r : []);
    setPolicies(Array.isArray(p) ? p : []);
  }

  useEffect(() => {
    void refresh().catch((err) => setError(String(err)));
  }, [client, session?.projectId]);

  async function createRole(e: FormEvent) {
    e.preventDefault();
    if (!client) return;
    try {
      await client.createRole(slug.trim() || name, name);
      await refresh();
    } catch (err) {
      setError(String(err));
    }
  }

  return (
    <>
      <PageHeader
        title="Access"
        subtitle="RBAC roles, bindings, and PBAC policy documents."
      />
      {!session?.projectId ? (
        <div className="panel error">Set a project ID in Settings.</div>
      ) : null}
      {error ? <div className="error">{error}</div> : null}
      <div className="grid-2">
        <div className="panel">
          <h2>Roles</h2>
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Slug</th>
              </tr>
            </thead>
            <tbody>
              {roles.map((role) => (
                <tr key={role.roleId}>
                  <td>{role.name}</td>
                  <td className="mono">{role.slug}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <form className="stack" style={{ marginTop: "1rem" }} onSubmit={createRole}>
            <div className="row">
              <label className="field">
                Name
                <input value={name} onChange={(e) => setName(e.target.value)} />
              </label>
              <label className="field">
                Slug
                <input value={slug} onChange={(e) => setSlug(e.target.value)} />
              </label>
            </div>
            <button type="submit">Create role</button>
          </form>
        </div>
        <div className="panel">
          <h2>Policies</h2>
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {policies.map((policy) => (
                <tr key={policy.policyId}>
                  <td>{policy.name}</td>
                  <td>
                    <span className="badge">{policy.status ?? "draft"}</span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <p style={{ color: "var(--muted)", fontSize: "0.85rem" }}>
            Author detailed PBAC documents via CLI YAML or API; this view lists project policies.
          </p>
        </div>
      </div>
    </>
  );
}
