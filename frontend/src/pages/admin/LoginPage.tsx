import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api, ApiError } from '../../api/http';
import { setAdminToken } from '../../lib/storage';

export default function LoginPage() {
  const navigate = useNavigate();
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true); setError(null);
    try {
      const r = await api<{ token: string; expiresAt: string }>('/api/auth/login', { json: { password }, noRedirect: true });
      setAdminToken(r.token, r.expiresAt);
      navigate('/admin', { replace: true });
    } catch (ex) {
      const status = ex instanceof ApiError ? ex.status : 0;
      setError(status === 429 ? 'Troppi tentativi. Aspetta un minuto.' : status === 401 ? 'Password errata.' : 'Server non raggiungibile.');
    } finally { setBusy(false); }
  }

  return (
    <main className="mx-auto flex min-h-screen max-w-sm flex-col justify-center px-6">
      <h1 className="mb-6 text-3xl font-extrabold tracking-tight text-chalk">Regia QuizLive</h1>
      <form onSubmit={submit} className="space-y-4">
        <label className="block text-sm text-mute">Password
          <input type="password" autoFocus value={password} onChange={(e) => setPassword(e.target.value)}
            className="mt-1 w-full rounded-xl border-2 border-line bg-panel px-4 py-3 text-lg outline-none focus:border-chalk" />
        </label>
        {error && <p className="text-sm text-bad" role="alert">{error}</p>}
        <button disabled={busy || !password} className="w-full rounded-xl bg-chalk py-3 text-lg font-bold text-ink disabled:opacity-50">
          {busy ? 'Accesso…' : 'Entra'}
        </button>
      </form>
    </main>
  );
}
