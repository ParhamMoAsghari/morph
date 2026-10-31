import type { ReactNode } from "react";
import { Navigate, Route, Routes } from "react-router-dom";
import { Layout } from "./components/Layout";
import { useMorph } from "./lib/MorphContext";
import { AccessPage } from "./pages/AccessPage";
import { ApiPage } from "./pages/ApiPage";
import { CollectionsPage } from "./pages/CollectionsPage";
import { FunctionsPage } from "./pages/FunctionsPage";
import { LoginPage } from "./pages/LoginPage";
import { OverviewPage } from "./pages/OverviewPage";
import { PluginsPage } from "./pages/PluginsPage";
import { ProcessesPage } from "./pages/ProcessesPage";
import { RecordsPage } from "./pages/RecordsPage";
import { SettingsPage } from "./pages/SettingsPage";
import { StoragePage } from "./pages/StoragePage";
import { WebhooksPage } from "./pages/WebhooksPage";

function RequireAuth({ children }: { children: ReactNode }) {
  const { session } = useMorph();
  if (!session) return <Navigate to="/login" replace />;
  return children;
}

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        path="/"
        element={
          <RequireAuth>
            <Layout />
          </RequireAuth>
        }
      >
        <Route index element={<OverviewPage />} />
        <Route path="collections" element={<CollectionsPage />} />
        <Route path="records" element={<RecordsPage />} />
        <Route path="access" element={<AccessPage />} />
        <Route path="storage" element={<StoragePage />} />
        <Route path="processes" element={<ProcessesPage />} />
        <Route path="functions" element={<FunctionsPage />} />
        <Route path="plugins" element={<PluginsPage />} />
        <Route path="api" element={<ApiPage />} />
        <Route path="webhooks" element={<WebhooksPage />} />
        <Route path="settings" element={<SettingsPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
