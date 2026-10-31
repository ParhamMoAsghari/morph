import { MorphClient } from "@morph/sdk";
import {
  createContext,
  useContext,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { clearSession, loadSession, saveSession, type SessionState } from "./session";

interface MorphContextValue {
  client: MorphClient | null;
  session: SessionState | null;
  connect: (session: SessionState) => void;
  updateScope: (projectId: string, environmentId: string) => void;
  logout: () => void;
}

const MorphContext = createContext<MorphContextValue | null>(null);

function createClient(session: SessionState): MorphClient {
  return new MorphClient({
    baseUrl: session.baseUrl,
    accessToken: session.accessToken,
    projectId: session.projectId || undefined,
    environmentId: session.environmentId || undefined,
  });
}

export function MorphProvider({ children }: { children: ReactNode }) {
  const initial = loadSession();
  const [session, setSession] = useState<SessionState | null>(initial);
  const [client, setClient] = useState<MorphClient | null>(
    initial ? createClient(initial) : null,
  );

  const value = useMemo<MorphContextValue>(
    () => ({
      client,
      session,
      connect(next) {
        saveSession(next);
        setSession(next);
        setClient(createClient(next));
      },
      updateScope(projectId, environmentId) {
        setSession((prev) => {
          if (!prev) return prev;
          const next = { ...prev, projectId, environmentId };
          saveSession(next);
          setClient(createClient(next));
          return next;
        });
      },
      logout() {
        clearSession();
        setSession(null);
        setClient(null);
      },
    }),
    [client, session],
  );

  return <MorphContext.Provider value={value}>{children}</MorphContext.Provider>;
}

export function useMorph(): MorphContextValue {
  const ctx = useContext(MorphContext);
  if (!ctx) throw new Error("useMorph outside provider");
  return ctx;
}
