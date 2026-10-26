import { useEffect, useState } from 'react';
import { useAdminHub } from '../../realtime/useQuizHub';
import { useAdminStore } from '../../store/adminStore';
import { api } from '../../api/http';
import type { GamePhase, QuestionAdminDto } from '../../api/types';
import ConnectionBadge from '../../components/ConnectionBadge';
import TeamTable from '../../components/TeamTable';
import MediaView from '../../components/MediaView';

const STEPS: { method: string; label: string; from: GamePhase[] }[] = [
  { method: 'NextQuestion', label: 'Prossima domanda', from: ['Lobby', 'AnswerRevealed', 'Leaderboard'] },
  { method: 'ShowQuestion', label: 'Mostra domanda', from: ['Ready'] },
  { method: 'OpenAnswers', label: 'Rileva risposte', from: ['QuestionShown'] },
  { method: 'CloseAnswers', label: 'Chiudi risposte', from: ['AnswersOpen'] },
  { method: 'RevealAnswer', label: 'Mostra risposta corretta', from: ['AnswersOpen', 'AnswersClosed'] },
  { method: 'ShowLeaderboard', label: 'Mostra classifica', from: ['Lobby', 'AnswerRevealed', 'Finished'] },
];

const PHASE_LABEL: Record<GamePhase, string> = {
  Lobby: 'Lobby — iscrizioni aperte', Ready: 'Domanda pronta', QuestionShown: 'Domanda mostrata',
  AnswersOpen: 'Risposte aperte', AnswersClosed: 'Risposte chiuse', AnswerRevealed: 'Risposta mostrata',
  Leaderboard: 'Classifica', Finished: 'Serata conclusa',
};

export default function DashboardPage() {
  const { invoke } = useAdminHub();
  const { state, status, error, busy } = useAdminStore();
  const [questions, setQuestions] = useState<QuestionAdminDto[]>([]);
  const [goTo, setGoTo] = useState('');

  const total = state?.game.question?.total;
  useEffect(() => {
    api<QuestionAdminDto[]>('/api/admin/questions').then(setQuestions).catch(() => {});
  }, [total, state?.nextQuestionPreview]);

  if (!state)
    return <main className="p-8 text-mute">Connessione alla regia… <ConnectionBadge status={status} /></main>;

  const g = state.game;
  const q = g.question;
  const answeredTeams = state.teams.filter((t) => t.isActive);

  return (
    <main className="mx-auto max-w-6xl space-y-6 p-4">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-extrabold">{g.quizName}</h1>
          <p className="text-chalk">{PHASE_LABEL[g.phase]}</p>
        </div>
        <ConnectionBadge status={status} />
      </header>

      {error && <p className="rounded-xl border border-bad/50 bg-bad/10 p-3 text-bad" role="alert">{error}</p>}

      <section aria-label="Controlli" className="rounded-2xl border border-line bg-panel p-4">
        <div className="flex flex-wrap gap-2">
          {STEPS.map((s) => {
            const enabled = s.from.includes(g.phase) && !busy;
            return (
              <button key={s.method} disabled={!enabled} onClick={() => void invoke(s.method)}
                className={`rounded-xl px-4 py-3 font-bold transition ${enabled ? 'bg-chalk text-ink hover:brightness-110' : 'bg-ink text-mute opacity-50'}`}>
                {s.label}
              </button>
            );
          })}
        </div>
        <div className="mt-3 flex flex-wrap items-center gap-2 border-t border-line pt-3 text-sm">
          <select value={goTo} onChange={(e) => setGoTo(e.target.value)} className="rounded-lg border border-line bg-ink px-3 py-2">
            <option value="">Vai alla domanda…</option>
            {questions.map((x) => <option key={x.id} value={x.id}>{x.orderIndex}. {x.text.slice(0, 50)}</option>)}
          </select>
          <button disabled={!goTo || busy} className="rounded-lg border border-line px-3 py-2 disabled:opacity-40"
            onClick={() => { if (window.confirm('Passare a questa domanda? La fase tornerà a "Domanda pronta".')) void invoke('GoToQuestion', goTo); }}>
            Vai
          </button>
          <button disabled={g.phase === 'Finished' || busy} className="ml-auto rounded-lg border border-bad/60 px-3 py-2 text-bad disabled:opacity-40"
            onClick={() => { if (window.confirm('Terminare la serata e mostrare la classifica finale?')) void invoke('FinishQuiz'); }}>
            Termina serata
          </button>
        </div>
      </section>

      <section className="grid gap-4 md:grid-cols-[2fr_1fr]" aria-label="Domanda corrente">
        <div className="rounded-2xl border border-line bg-panel p-4">
          {q ? (
            <>
              <p className="text-sm text-mute">Domanda {q.index} di {q.total}</p>
              <h2 className="mt-1 text-xl font-bold">{q.text}</h2>
              {q.mediaType !== 'None' && q.mediaUrl && (
                <div className="mt-3 h-40 overflow-hidden rounded-lg bg-black/30"><MediaView type={q.mediaType} url={q.mediaUrl} controls /></div>
              )}
              <ul className="mt-3 grid gap-2 sm:grid-cols-2">
                {q.options.map((o) => {
                  const ok = o.label === state.currentCorrectLabel;
                  return (
                    <li key={o.label} className={`rounded-lg border px-3 py-2 ${ok ? 'border-ok bg-ok/15 font-bold' : 'border-line'}`}>
                      <b className="mr-2">{o.label}</b>{o.text}{ok && <span className="ml-2 text-ok">✓ corretta</span>}
                    </li>
                  );
                })}
              </ul>
              {q.mediaType === 'None' && g.phase === 'Ready' && <p className="mt-3 text-xs text-mute">Domanda non ancora visibile sul Display.</p>}
            </>
          ) : (
            <p className="text-mute">Nessuna domanda selezionata. Premi "Prossima domanda".</p>
          )}
        </div>
        <div className="space-y-3 rounded-2xl border border-line bg-panel p-4">
          <div>
            <p className="text-sm text-mute">Risposte</p>
            <p className="text-4xl font-extrabold">{g.answeredCount}<span className="text-xl text-mute"> / {g.activeTeams}</span></p>
            <div className="mt-2 h-2 overflow-hidden rounded-full bg-ink">
              <div className="h-full bg-chalk transition-all" style={{ width: `${g.activeTeams ? (g.answeredCount / g.activeTeams) * 100 : 0}%` }} />
            </div>
          </div>
          <div>
            <p className="text-sm text-mute">Tavoli attivi</p>
            <p className="text-2xl font-bold">{answeredTeams.length}</p>
          </div>
          {state.nextQuestionPreview && (
            <div>
              <p className="text-sm text-mute">Prossima</p>
              <p className="text-sm">{state.nextQuestionPreview}</p>
            </div>
          )}
        </div>
      </section>

      <section aria-label="Tavoli">
        <h2 className="mb-2 text-lg font-bold">Tavoli</h2>
        <TeamTable teams={state.teams} phase={g.phase} invoke={invoke} busy={busy} />
      </section>
    </main>
  );
}
