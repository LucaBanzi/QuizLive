const SESSION_KEY = 'quizlive.session';
const ADMIN_KEY = 'quizlive.admin';

export interface StoredSession { teamId: string; nickname: string; sessionToken: string; savedAt: number }

export function loadSession(): StoredSession | null {
  try {
    const raw = localStorage.getItem(SESSION_KEY);
    if (!raw) return null;
    const s = JSON.parse(raw) as StoredSession;
    return s?.nickname && s?.sessionToken ? s : null;
  } catch { return null; }
}
export function saveSession(s: StoredSession) {
  try { localStorage.setItem(SESSION_KEY, JSON.stringify(s)); } catch { /* storage non disponibile */ }
}
export function clearSession() {
  try { localStorage.removeItem(SESSION_KEY); } catch { /* ignore */ }
}

export function getAdminToken(): string | null {
  try {
    const raw = sessionStorage.getItem(ADMIN_KEY);
    if (!raw) return null;
    const { token, expiresAt } = JSON.parse(raw) as { token: string; expiresAt: string };
    if (new Date(expiresAt).getTime() < Date.now()) { sessionStorage.removeItem(ADMIN_KEY); return null; }
    return token;
  } catch { return null; }
}
export function setAdminToken(token: string, expiresAt: string) {
  sessionStorage.setItem(ADMIN_KEY, JSON.stringify({ token, expiresAt }));
}
export function clearAdminToken() { sessionStorage.removeItem(ADMIN_KEY); }
