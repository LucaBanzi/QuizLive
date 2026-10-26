import { useState, type MutableRefObject } from 'react';
import { Link } from 'react-router-dom';
import type { HubSession } from '../../realtime/hubClient';
import type { RegisterResult } from '../../api/types';
import { usePlayerStore } from '../../store/playerStore';
import { errCode, errMessage } from '../../lib/format';
import ConnectionBadge from '../../components/ConnectionBadge';

export default function RegisterPage({ hub }: { hub: MutableRefObject<HubSession | null> }) {
  const { status, notice, login } = usePlayerStore();
  const [nickname, setNickname] = useState('');
  const [spokes, setSpokes] = useState('');
  const [table, setTable] = useState('');
  const [errors, setErrors] = useState<{ nickname?: string; spokes?: string; table?: string; form?: string }>({});
  const [sending, setSending] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    const n = nickname.trim(); const s = spokes.trim(); const t = parseInt(table, 10);
    const err: typeof errors = {};
    if (n.length < 2 || n.length > 24) err.nickname = 'Da 2 a 24 caratteri.';
    if (s.length < 2) err.spokes = 'Inserisci nome e cognome.';
    if (!Number.isInteger(t) || t < 1 || t > 99) err.table = 'Numero da 1 a 99.';
    setErrors(err);
    if (Object.keys(err).length) return;

    setSending(true);
    try {
      const r = await hub.current!.conn.invoke<RegisterResult>('RegisterTeam', { nickname: n, spokespersonName: s, tableNumber: t });
      login(r);
    } catch (ex) {
      const code = errCode(ex);
      if (code === 'NICKNAME_TAKEN') setErrors({ nickname: errMessage(code) });
      else if (code === 'TABLE_TAKEN') setErrors({ table: errMessage(code) });
      else setErrors({ form: errMessage(code) });
    } finally { setSending(false); }
  }

  const field = 'mt-1 w-full rounded-xl border-2 border-line bg-panel px-4 py-3 text-lg outline-none focus:border-chalk';

  return (
    <main className="mx-auto flex min-h-[100dvh] max-w-md flex-col justify-center px-6 py-8">
      <div className="mb-6 flex items-center justify-between">
        <h1 className="text-3xl font-extrabold tracking-tight">Siediti, si gioca</h1>
        <ConnectionBadge status={status} />
      </div>

      {notice && <p className="mb-4 rounded-xl bg-panel p-3 text-sm text-chalk" role="alert">{notice}</p>}

      <form onSubmit={submit} className="space-y-4" noValidate>
        <label className="block text-sm text-mute">Nome tavolo / Nickname
          <input className={field} value={nickname} maxLength={24} autoComplete="off" onChange={(e) => setNickname(e.target.value)} />
          {errors.nickname && (
            <span className="mt-1 block text-sm text-bad">
              {errors.nickname}{' '}
              {errors.nickname.includes('in uso') && <Link to="/recover" className="underline">Riprendi un tavolo esistente</Link>}
            </span>
          )}
        </label>
        <label className="block text-sm text-mute">Nome e cognome del portavoce
          <input className={field} value={spokes} maxLength={80} autoComplete="name" onChange={(e) => setSpokes(e.target.value)} />
          {errors.spokes && <span className="mt-1 block text-sm text-bad">{errors.spokes}</span>}
        </label>
        <label className="block text-sm text-mute">Numero del tavolo
          <input className={field} value={table} inputMode="numeric" pattern="[0-9]*" maxLength={2}
            onChange={(e) => setTable(e.target.value.replace(/\D/g, ''))} />
          {errors.table && <span className="mt-1 block text-sm text-bad">{errors.table}</span>}
        </label>
        {errors.form && <p className="text-sm text-bad" role="alert">{errors.form}</p>}

        <button type="submit" disabled={sending || status !== 'connected'}
          className="w-full rounded-2xl bg-chalk py-4 text-xl font-extrabold text-ink disabled:opacity-50">
          {status !== 'connected' ? 'Connessione…' : sending ? 'Un attimo…' : 'Entra'}
        </button>
      </form>

      <Link to="/recover" className="mt-6 text-center text-sm text-mute underline">Riprendi un tavolo esistente</Link>
    </main>
  );
}
