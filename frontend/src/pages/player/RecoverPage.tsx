import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { usePlayerHub } from '../../realtime/useQuizHub';
import { usePlayerStore } from '../../store/playerStore';
import type { RejoinResult } from '../../api/types';
import { errCode, errMessage } from '../../lib/format';
import ConnectionBadge from '../../components/ConnectionBadge';

/** Recupero via nickname: funziona solo se il presentatore ha aperto la finestra di recupero ("Sblocca"). */
export default function RecoverPage() {
  const hub = usePlayerHub(false);
  const { status, login } = usePlayerStore();
  const navigate = useNavigate();
  const [nickname, setNickname] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setSending(true); setError(null);
    try {
      const r = await hub.current!.conn.invoke<RejoinResult>('Rejoin', { nickname: nickname.trim(), sessionToken: null });
      login(r);                       // salva anche in localStorage
      navigate('/', { replace: true });
    } catch (ex) {
      const code = errCode(ex);
      setError(code === 'NOT_AUTHORIZED' ? 'Nickname non trovato. Scrivilo esattamente come lo avevi scelto.' : errMessage(code));
    } finally { setSending(false); }
  }

  return (
    <main className="mx-auto flex min-h-[100dvh] max-w-md flex-col justify-center px-6">
      <div className="mb-4 flex items-center justify-between">
        <h1 className="text-2xl font-extrabold">Riprendi il tuo tavolo</h1>
        <ConnectionBadge status={status} />
      </div>
      <p className="mb-5 text-mute">Hai cambiato telefono? Chiedi al presentatore di sbloccare il tavolo, poi scrivi qui il nickname esatto.</p>
      <form onSubmit={submit} className="space-y-4">
        <input className="w-full rounded-xl border-2 border-line bg-panel px-4 py-3 text-lg outline-none focus:border-chalk"
          value={nickname} onChange={(e) => setNickname(e.target.value)} placeholder="Nickname del tavolo" autoComplete="off" />
        {error && <p className="text-sm text-bad" role="alert">{error}</p>}
        <button type="submit" disabled={sending || !nickname.trim() || status !== 'connected'}
          className="w-full rounded-2xl bg-chalk py-4 text-xl font-extrabold text-ink disabled:opacity-50">
          {sending ? 'Un attimo…' : 'Riprendi'}
        </button>
      </form>
      <Link to="/" className="mt-6 text-center text-sm text-mute underline">Torna alla registrazione</Link>
    </main>
  );
}
