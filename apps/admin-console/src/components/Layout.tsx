import { NavLink, Outlet } from "react-router-dom";
import { useMorph } from "../lib/MorphContext";

const links = [
  ["", "Overview"],
  ["collections", "Collections"],
  ["records", "Records"],
  ["access", "Access"],
  ["storage", "Storage"],
  ["processes", "Processes"],
  ["functions", "Functions"],
  ["plugins", "Plugins"],
  ["api", "API & SQL"],
  ["webhooks", "Webhooks"],
  ["settings", "Settings"],
] as const;

export function Layout() {
  const { session, logout } = useMorph();

  return (
    <div className="shell">
      <aside className="sidebar">
        <div className="brand">
          <div className="brand-mark">Morph</div>
          <div className="brand-sub">Developer panel</div>
        </div>
        <nav className="nav">
          {links.map(([to, label]) => (
            <NavLink
              key={to}
              to={to ? `/${to}` : "/"}
              end={to === ""}
              className={({ isActive }) => (isActive ? "active" : undefined)}
            >
              {label}
            </NavLink>
          ))}
        </nav>
        <div className="sidebar-foot">
          <div>{session?.email}</div>
          <div style={{ marginTop: 6 }}>{session?.baseUrl}</div>
          <button className="secondary" style={{ marginTop: 12 }} onClick={logout}>
            Sign out
          </button>
        </div>
      </aside>
      <main className="main">
        <Outlet />
      </main>
    </div>
  );
}
