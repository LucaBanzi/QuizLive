import { useEffect, useLayoutEffect, type MutableRefObject } from 'react';
import { usePlayerHub } from '../../realtime/useQuizHub';
import { usePlayerStore } from '../../store/playerStore';
import { loadSession } from '../../lib/storage';
import { uuid } from '../../lib/format';
import type { AnswerAckDto, Label, PlayerStateDto } from '../../api/types';
import type { HubSession } from '../../realtime/hubClient';
import AnswerButton, { type BtnState } from '../../components/AnswerButton';
import ConnectionBadge from '../../components/ConnectionBadge';
import RegisterPage from './RegisterPage';

const QUESTION_PHASES = ['QuestionShown', 'AnswersOpen', 'AnswersClosed', 'AnswerRevealed'];

/** Pagina "/" del giocatore: ripristina la sessione, mostra il form o la schermata di gioco. */
export default function GamePage() {
  const hub = usePlayerHub(true);
  const boot = usePlayerStore((s) => s.boot);

  // Prima di mostrare qualsiasi form: se c'è una sessione salvata si resta in "Ricollegamento in corso…"
  useLayoutEffect(() => {
    const st = usePlayerStore.getState();
    if (st.boot !== 'ready') st.setBoot(loadSession() ? 'booting' : 'needsRegister');
  }, []);

  if (boot === 'booting')
    return <main className="flex min-h-[100dvh] items-center justify-center text-xl text-mute">Ricollegamento in corso…</main>;
  if (boot === 'needsRegister') return <RegisterPage hub={hub} />;
  return <GameScreen hub={hub} />;
}

function GameScreen({ hub }: { hub: MutableRefObject<HubSession | null> }) {
  const { state, status, pending, baseScore, notice, setPending, setNotice } = usePlayerStore();

  // Wake Lock (con fallback silenzioso) per ridurre i blocchi schermo
  useEffect(() => {
    let lock: { release: () => Promise<void> } | null = null;
    const request = async () => {
      try { lock = await (navigator as any).wakeLock?.request('screen'); } catch { /* non supportato */ }
    };
    void request();
    const onVis = () => { if (document.visibilityState === 'visible') void request(); };
    document.addEventListener('visibilitychange', onVis);
    return () => { document.removeEventListener('visibilitychange', onVis); void lock?.release().catch(() => {}); };
  }, []);

  if (!state) return <main className="flex min-h-[100dvh] items-center justify-center text-mute">Caricamento…</main>;

  const connected = status === 'connected';
  const inQuestion = QUESTION_PHASES.includes(state.phase);
  const canAnswer = connected && state.phase === 'AnswersOpen' && !state.myAnswer && !pending && !!state.questionId;

  async function resync() {
    try { usePlayerStore.getState().applyState(await hub.current!.conn.invoke<PlayerStateDto>('GetMyState')); } catch { /* ignore */ }
  }

  async function send(label: Label) {
    if (!canAnswer || !state?.questionId) return;
    setPending(label);                         // tutti i pulsanti si bloccano subito, prima della risposta del server
    setNotice(null);
    try {
      const ack = await hub.current!.conn.invoke<AnswerAckDto>('SubmitAnswer', {
        questionId: state.questionId, label, clientRequestId: uuid(),
      });
      if (ack.status === 'Accepted' || ack.status === 'AlreadyAnswered') {
        if (ack.status === 'Accepted') navigator.vibrate?.(80);
        // se era già registrata una lettera diversa, vale quella del server
        usePlayerStore.setState((s) => ({ pending: null, state: s.state ? { ...s.state, myAnswer: ack.storedLabel } : s.state }));
      } else {
        setPending(null);
        if (ack.status === 'Closed') setNotice('Tempo scaduto');
        else await resync();
      }
    } catch {
      setPending(null);       // esito incerto: lo stato vero arriva con la risincronizzazione
      await resync();
    }
  }

  function btnState(l: Label): BtnState {
    if (state!.phase === 'AnswerRevealed') {
      if (l === state!.correctLabel) return 'correct';
      if (l === state!.myAnswer) return 'wrong';
      return 'dim';
    }
    if (pending === l) return 'sending';
    if (state!.myAnswer === l) return 'selected';
    if (canAnswer) return 'idle';
    return state!.myAnswer ? 'dim' : 'disabled';
  }

  // ------- riga di stato in alto
  let line = 'In attesa della domanda';
  if (!connected) line = 'Riconnessione…';
  else if (state.phase === 'QuestionShown') line = 'Leggi la domanda sullo schermo';
  else if (state.phase === 'AnswersOpen') line = pending ? 'Invio…' : state.myAnswer ? 'Risposta registrata' : 'Rispondi!';
  else if (state.phase === 'AnswersClosed') line = state.myAnswer ? 'Risposta registrata' : 'Tempo scaduto';
  else if (state.phase === 'AnswerRevealed') {
    const gained = state.score - baseScore;
    line = state.isCorrect ? `Corretta!${gained > 0 ? ` +${gained}` : ''}` : state.myAnswer ? 'Sbagliata' : 'Nessuna risposta';
  } else if (state.phase === 'Leaderboard') line = 'Guarda la classifica sullo schermo';
  else if (state.phase === 'Finished') line = 'Serata conclusa';
  else if (state.phase === 'Lobby') line = 'Sei dentro! Si parte a breve';
  if (notice && state.phase !== 'AnswerRevealed') line = notice;

  const lineColor =
    state.phase === 'AnswerRevealed' ? (state.isCorrect ? 'text-ok' : 'text-bad') : state.phase === 'AnswersOpen' && canAnswer ? 'text-chalk' : 'text-paper';

  return (
    <main className="flex h-[100dvh] flex-col overflow-hidden">
      <header className="flex items-center justify-between gap-3 px-4 pb-2 pt-3">
        <span className="truncate text-sm text-mute">{state.nickname}</span>
        <ConnectionBadge status={status} />
      </header>
      <p className={`px-4 pb-3 text-center text-2xl font-extrabold ${lineColor}`} role="status" aria-live="polite">{line}</p>

      {inQuestion ? (
        <section className="grid min-h-0 flex-1 grid-cols-2 grid-rows-2 gap-3 p-3 pb-5" aria-label="Risposte">
          {(['A', 'B', 'C', 'D'] as Label[]).map((l) => (
            <AnswerButton key={l} label={l} state={btnState(l)} onClick={() => void send(l)} />
          ))}
        </section>
      ) : (
        <section className="flex flex-1 flex-col items-center justify-center gap-2 px-6 text-center">
          {state.phase === 'Finished' ? (
            <>
              <p className="text-mute">Classifica finale</p>
              <p className="text-7xl font-extrabold text-chalk">{state.rank}°</p>
            </>
          ) : state.questionIndex > 0 ? (
            <p className="text-mute">Domanda {state.questionIndex} di {state.totalQuestions}</p>
          ) : null}
          <p className="mt-6 text-sm text-mute">Punti {state.score} · Posizione {state.rank > 0 ? `${state.rank}°` : '—'}</p>
        </section>
      )}

      {inQuestion && (
        <footer className="pb-3 text-center text-xs text-mute">Punti {state.score} · Posizione {state.rank > 0 ? `${state.rank}°` : '—'}</footer>
      )}
    </main>
  );
}
