import { clearAdminToken, getAdminToken } from '../lib/storage';

export class ApiError extends Error {
  constructor(public status: number, public code?: string, message?: string) {
    super(message ?? code ?? `HTTP ${status}`);
  }
}

interface Opts { method?: string; json?: unknown; body?: BodyInit; noRedirect?: boolean }

export async function api<T = unknown>(path: string, opts: Opts = {}): Promise<T> {
  const headers: Record<string, string> = {};
  const token = getAdminToken();
  if (token) headers.Authorization = `Bearer ${token}`;
  let body = opts.body;
  if (opts.json !== undefined) { headers['Content-Type'] = 'application/json'; body = JSON.stringify(opts.json); }

  const res = await fetch(path, { method: opts.method ?? (body ? 'POST' : 'GET'), headers, body });
  if (res.status === 401 && !opts.noRedirect) {
    clearAdminToken();
    window.location.assign('/admin/login');
    throw new ApiError(401, 'UNAUTHORIZED');
  }
  if (!res.ok) {
    let code: string | undefined;
    try { code = (await res.json())?.error; } catch { /* corpo non JSON */ }
    throw new ApiError(res.status, code, code);
  }
  if (res.status === 204) return undefined as T;
  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

export async function downloadFile(path: string, fallbackName: string) {
  const token = getAdminToken();
  const res = await fetch(path, { headers: token ? { Authorization: `Bearer ${token}` } : {} });
  if (res.status === 401) { clearAdminToken(); window.location.assign('/admin/login'); return; }
  if (!res.ok) throw new ApiError(res.status);
  const blob = await res.blob();
  const cd = res.headers.get('Content-Disposition') ?? '';
  const name = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(cd)?.[1] ?? fallbackName;
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url; a.download = name; a.click();
  URL.revokeObjectURL(url);
}
