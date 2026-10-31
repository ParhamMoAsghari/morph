const KEY = "morph.devpanel.session.v1";

export interface SessionState {
  baseUrl: string;
  accessToken: string;
  email: string;
  projectId: string;
  environmentId: string;
}

export function loadSession(): SessionState | null {
  try {
    const raw = localStorage.getItem(KEY);
    if (!raw) return null;
    return JSON.parse(raw) as SessionState;
  } catch {
    return null;
  }
}

export function saveSession(session: SessionState): void {
  localStorage.setItem(KEY, JSON.stringify(session));
}

export function clearSession(): void {
  localStorage.removeItem(KEY);
}
