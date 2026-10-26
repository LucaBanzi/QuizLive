import { useState } from 'react';
import type { GamePhase, TeamAdminDto } from '../api/types';
import ConnectionBadge from './ConnectionBadge';

type Invoke = (method: string, ...args: unknown[]) => Promise<unknown>;
type Dialog =
  | { kind: 'unlock'; team: TeamAdminDto }
  | { kind: 'transfer'; team: TeamAdminDto }
  | { kind: 'adjust'; team: TeamAdminDto }
  | { kind: 'toggle'; team: TeamAdminDto };

interface Props { teams: TeamAdminDto[]; phase: GamePhase; invoke: Invoke; busy: boolean }

export default function TeamTable({ teams, phase, invoke, busy }: Props) {
  const [dialog, setDialog] = useState<Dialog | null>(null);
  const answersPhase = phase === 'AnswersOpen' || phase === 'AnswersClosed';
  const close = () => setDialog(null);

  return (
    <>
      <div className="overflow-x-auto rounded-xl border border-line">
        <table className="w-full min-w-[760px] text-left text-sm">
          <thead className="bg-panel text-mute">
            <tr>
              <th className="p-3">Tav.</th><th className="p-3">Nickname</th><th className="p-3">Portavoce</th>
              <th className="p-3 text-right">Punti</th><th className="p-3">Stato</th><th className="p-3">Risposta</th>
              <th className="p-3">Azioni</th>
            </tr>
          </thead>
          <tbody>
            {teams.length === 0 && (
              <tr><td colSpan={7} className="p-6 text-center text-mute">Nessun tavolo iscritto. Mostra il QR sul Display.</td></tr>
            )}
            {teams.map((t) => (
              <tr key={t.id} className={`border-t border-line ${t.isActive ? '' : 'opacity-50'}`}>
                <td className="p-3 font-bold">{t.tableNumber}</td>
                <td className="p-3">{t.nickname}</td>
                <td className="p-3 text-mute">{t.spokespersonName}</td>
                <td className="p-3 text-right font-bold">{t.score}</td>
                <td className="p-3">
                  <ConnectionBadge status={t.isConnected ? 'connected' : 'disconnected'} />
                  {!t.isActive && <span className="ml-2 text-xs text-bad">disattivato</span>}
                  {t.recoveryOpenUntil && new Date(t.recoveryOpenUntil) > new Date() && (
                    <span className="ml-2 text-xs text-ansC">recupero aperto</span>
                  )}
                </td>
                <td className="p-3">
                  {t.answeredCurrent ? <b className="rounded bg-line px-2 py-0.5">{t.selectedLabel}</b> : <span className="text-mute">—</span>}
                </td>
                <td className="p-3">
                  <div className="flex flex-wrap gap-1.5">
                    <Btn onClick={() => setDialog({ kind: 'adjust', team: t })}>± Punti</Btn>
                    <Btn onClick={() => setDialog({ kind: 'unlock', team: t })}>Sblocca</Btn>
                    <Btn onClick={() => setDialog({ kind: 'transfer', team: t })}>Trasferisci</Btn>
                    <Btn onClick={() => setDialog({ kind: 'toggle', team: t })}>{t.isActive ? 'Disattiva' : 'Riattiva'}</Btn>
                    {answersPhase && t.answeredCurrent && (
                      <Btn onClick={() => void invoke('ResetTeamAnswer', t.id)} disabled={busy}>Annulla risposta</Btn>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {dialog && <Modal onClose={close}><DialogBody dialog={dialog} teams={teams} invoke={invoke} close={close} /></Modal>}
    </>
  );
}

function Btn({ children, onClick, disabled }: { children: React.ReactNode; onClick: () => void; disabled?: boolean }) {
  return (
    <button type="button" onClick={onClick} disabled={disabled}
      className="rounded-md border border-line bg-panel px-2.5 py-1 text-xs hover:border-chalk disabled:opacity-40">
      {children}
    </button>
  );
}

function Modal({ children, onClose }: { children: React.ReactNode; onClose: () => void }) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4" onClick={onClose} role="dialog" aria-modal>
      <div className="w-full max-w-md rounded-2xl border border-line bg-panel p-5" onClick={(e) => e.stopPropagation()}>{children}</div>
    </div>
  );
}

function DialogBody({ dialog, teams, invoke, close }: { dialog: Dialog; teams: TeamAdminDto[]; invoke: Invoke; close: () => void }) {
  const t = dialog.team;
  const [delta, setDelta] = useState('');
  const [reason, setReason] = useState('');
  const [minutes, setMinutes] = useState('10');
  const [target, setTarget] = useState('');
  const [working, setWorking] = useState(false);

  const run = async (method: string, ...args: unknown[]) => { setWorking(true); await invoke(method, ...args); setWorking(false); close(); };
  const Actions = ({ ok, disabled }: { ok: () => void; disabled?: boolean }) => (
    <div className="mt-5 flex justify-end gap-2">
      <button className="rounded-lg px-4 py-2 text-mute" onClick={close}>Annulla</button>
      <button className="rounded-lg bg-chalk px-4 py-2 font-bold text-ink disabled:opacity-40" disabled={disabled || working} onClick={ok}>Conferma</button>
    </div>
  );
  const input = 'mt-1 w-full rounded-lg border border-line bg-ink px-3 py-2';

  if (dialog.kind === 'adjust') {
    const d = parseInt(delta, 10);
    return (
      <>
        <h3 className="text-lg font-bold">Assegna / detrai punti — {t.nickname}</h3>
        <label className="mt-4 block text-sm text-mute">Punti (negativi per detrarre)
          <input className={input} inputMode="numeric" value={delta} onChange={(e) => setDelta(e.target.value)} placeholder="es. 50 oppure -20" />
        </label>
        <label className="mt-3 block text-sm text-mute">Motivo (obbligatorio)
          <input className={input} maxLength={200} value={reason} onChange={(e) => setReason(e.target.value)} />
        </label>
        <Actions ok={() => run('AdjustScore', t.id, d, reason)} disabled={!Number.isInteger(d) || d === 0 || !reason.trim()} />
      </>
    );
  }
  if (dialog.kind === 'unlock')
    return (
      <>
        <h3 className="text-lg font-bold">Sbloccare {t.nickname}?</h3>
        <p className="mt-2 text-sm text-mute">
          Il telefono attuale viene scollegato subito. Dal nuovo telefono il giocatore sceglie "Riprendi un tavolo esistente"
          e inserisce il nickname esatto. Punteggio e risposte restano intatti.
        </p>
        <label className="mt-3 block text-sm text-mute">Finestra di recupero (minuti)
          <input className={input} inputMode="numeric" value={minutes} onChange={(e) => setMinutes(e.target.value)} />
        </label>
        <Actions ok={() => run('UnlockTeam', t.id, Math.max(1, parseInt(minutes, 10) || 10))} />
      </>
    );
  if (dialog.kind === 'transfer') {
    const others = teams.filter((x) => x.id !== t.id && x.isActive);
    return (
      <>
        <h3 className="text-lg font-bold">Trasferisci {t.nickname}</h3>
        <p className="mt-2 text-sm text-mute">
          Risposte e punti di questo tavolo passano al tavolo scelto (il nuovo nickname del giocatore). Questo tavolo viene disattivato.
        </p>
        <label className="mt-3 block text-sm text-mute">Tavolo di destinazione
          <select className={input} value={target} onChange={(e) => setTarget(e.target.value)}>
            <option value="">Seleziona…</option>
            {others.map((o) => <option key={o.id} value={o.id}>Tav. {o.tableNumber} — {o.nickname}</option>)}
          </select>
        </label>
        <Actions ok={() => run('TransferTeam', t.id, target)} disabled={!target} />
      </>
    );
  }
  return (
    <>
      <h3 className="text-lg font-bold">{t.isActive ? 'Disattivare' : 'Riattivare'} {t.nickname}?</h3>
      <p className="mt-2 text-sm text-mute">
        {t.isActive
          ? 'Il tavolo esce dalla classifica e il telefono viene scollegato.'
          : 'Il tavolo rientra in classifica. Per ricollegare il telefono usa poi "Sblocca".'}
      </p>
      <Actions ok={() => run('SetTeamActive', t.id, !t.isActive)} />
    </>
  );
}
