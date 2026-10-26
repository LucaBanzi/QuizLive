import { useState } from 'react';
import { useDisplayHub } from '../../realtime/useQuizHub';
import { useDisplayStore } from '../../store/displayStore';
import type { GameStateDto, Label } from '../../api/types';
import MediaView from '../../components/MediaView';
import QrBlock from '../../components/QrBlock';
import Leaderboard from '../../components/Leaderboard';
import ConnectionBadge from '../../components/ConnectionBadge';
import { SHAPE } from '../../components/AnswerButton';

const OPT_BG: Record<Label, string> = { A: 'bg-ansA', B: 'bg-ansB', C: 'bg-ansC', D: 'bg-ansD' };

export default function DisplayPage() {
  useDisplayHub();
  const [started, setStarted] = useState(false);
  const { game, status, answered, total, publicOrigin } = useDisplayStore();

  return (
    <div className="flex h-screen w-screen items-center justify-center overflow-hidden bg-black">
      <div className="stage bg-ink text-paper">
        {!game ? (
          <Center><span className="text-[3em] text-mute">Connessione al server…</span></Center>
        ) : (
          <Scene game={game} answered={answered} total={total} origin={publicOrigin || window.location.origin} />
        )}

        <div className="absolute right-[1.5em] top-[1.2em] opacity-80"><ConnectionBadge status={status} compact /></div>

        {/* Un click sblocca l'autoplay di audio e video del browser */}
        {!started && (
          <button type="button" onClick={() => setStarted(true)}
            className="absolute inset-0 z-50 flex flex-col items-center justify-center gap-[2em] bg-ink/95 text-[4em] font-extrabold text-chalk">
            Avvia Display
            <span className="text-[0.4em] font-normal text-mute">Clicca una volta per abilitare audio e video</span>
          </button>
        )}
      </div>
    </div>
  );
}

function Center({ children }: { children: React.ReactNode }) {
  return <div className="absolute inset-0 flex items-center justify-center">{children}</div>;
}

function Scene({ game, answered, total, origin }: { game: GameStateDto; answered: number; total: number; origin: string }) {
  switch (game.phase) {
    case 'Lobby': return <LobbyView game={game} origin={origin} />;
    case 'Ready': return <ReadyView game={game} />;
    case 'QuestionShown':
    case 'AnswersOpen':
    case 'AnswersClosed':
    case 'AnswerRevealed': return <QuestionView game={game} answered={answered} total={total} />;
    case 'Leaderboard': return <BoardView game={game} />;
    case 'Finished': return <FinalView game={game} />;
  }
}

// ------------------------------------------------------------------ Lobby
function LobbyView({ game, origin }: { game: GameStateDto; origin: string }) {
  const teams = game.leaderboard ?? [];
  const host = origin.replace(/^https?:\/\//, '');
  return (
    <div className="absolute inset-0 flex gap-[4em] p-[4em]">
      <div className="flex w-[34%] flex-col justify-center">
        <h1 className="text-[5.2em] font-extrabold leading-[1.05] tracking-tight text-chalk">{game.quizName}</h1>
        <p className="mt-[1em] text-[2.2em] text-mute">Inquadra il QR code con il telefono e iscrivi il tuo tavolo</p>
        <div className="mt-[1.6em] w-[17em]"><QrBlock url={origin} /></div>
        <p className="mt-[0.8em] break-all text-[1.7em] text-paper">{host}</p>
      </div>
      <div className="flex min-w-0 flex-1 flex-col">
        <p className="mb-[1em] text-[2.4em] font-bold">
          {teams.length === 0 ? 'Nessun tavolo ancora' : `${teams.length} ${teams.length === 1 ? 'tavolo iscritto' : 'tavoli iscritti'}`}
        </p>
        <div className="grid min-h-0 flex-1 auto-rows-min grid-cols-3 content-start gap-[1em] overflow-hidden">
          {[...teams].sort((a, b) => a.tableNumber - b.tableNumber).slice(0, 30).map((t) => (
            <div key={t.teamId} className="pop truncate rounded-[0.7em] bg-panel px-[1em] py-[0.7em] text-[1.9em] font-semibold">
              <span className="mr-[0.5em] text-chalk">{t.tableNumber}</span>{t.nickname}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

function ReadyView({ game }: { game: GameStateDto }) {
  return (
    <Center>
      <div className="text-center">
        <p className="text-[3em] text-mute">{game.quizName}</p>
        <p className="mt-[0.4em] text-[6em] font-extrabold text-chalk">Prossima domanda…</p>
      </div>
    </Center>
  );
}

// ------------------------------------------------------------------ Domanda
function QuestionView({ game, answered, total }: { game: GameStateDto; answered: number; total: number }) {
  const q = game.question;
  if (!q) return null;
  const reveal = game.reveal;
  const maxVotes = reveal ? Math.max(1, ...Object.values(reveal.votesByLabel)) : 1;
  const hasMedia = q.mediaType !== 'None' && !!q.mediaUrl;
  const showCount = game.phase === 'AnswersOpen' || game.phase === 'AnswersClosed';
  const pct = total > 0 ? Math.min(100, (answered / total) * 100) : 0;

  const status =
    game.phase === 'QuestionShown' ? 'Leggete la domanda'
      : game.phase === 'AnswersOpen' ? 'Rispondete ora!'
        : game.phase === 'AnswersClosed' ? 'Risposte chiuse'
          : 'Risposta corretta';

  return (
    <div className="absolute inset-0 flex flex-col gap-[2em] p-[3.2em]">
      <div className="flex items-baseline justify-between pr-[4em] text-[2em]">
        <span className="text-mute">Domanda {q.index} di {q.total}</span>
        <span className={`font-bold ${game.phase === 'AnswersOpen' ? 'text-chalk' : 'text-paper'}`}>{status}</span>
      </div>

      <div className={`flex min-h-0 flex-1 gap-[3em] ${hasMedia ? '' : 'items-center'}`}>
        <h2 className={`${hasMedia ? 'w-[52%] text-[3.4em]' : 'w-full text-[4.6em]'} font-extrabold leading-[1.12] tracking-tight`}>{q.text}</h2>
        {hasMedia && <div className="min-h-0 flex-1 overflow-hidden rounded-[1em] bg-black/30"><MediaView type={q.mediaType} url={q.mediaUrl} /></div>}
      </div>

      <div className="grid grid-cols-2 gap-[1.2em]">
        {q.options.map((o) => {
          const correct = reveal?.correctLabel === o.label;
          const votes = reveal?.votesByLabel[o.label] ?? 0;
          return (
            <div key={o.label}
              className={`relative flex items-center gap-[1em] overflow-hidden rounded-[0.9em] bg-panel px-[1em] py-[0.9em] transition-opacity duration-500 ${reveal && !correct ? 'opacity-35' : ''} ${correct ? 'ring-[0.35em] ring-ok' : ''}`}>
              {reveal && <div className="absolute inset-y-0 left-0 bg-white/10 transition-all duration-700" style={{ width: `${(votes / maxVotes) * 100}%` }} />}
              <span className={`relative flex h-[2.8em] w-[2.8em] shrink-0 items-center justify-center rounded-[0.5em] text-[2.2em] font-extrabold ${OPT_BG[o.label]} ${o.label === 'C' ? 'text-ink' : 'text-white'}`}>
                {o.label}<span className="ml-[0.2em] text-[0.45em]">{SHAPE[o.label]}</span>
              </span>
              <span className="relative min-w-0 flex-1 text-[2.3em] font-semibold leading-tight">{o.text}</span>
              {reveal && <span className="relative text-[2.2em] font-extrabold">{votes}</span>}
            </div>
          );
        })}
      </div>

      <div className="h-[5em]">
        {showCount && (
          <div>
            <p className="mb-[0.5em] text-[1.9em] text-mute">{answered}/{total} tavoli hanno risposto</p>
            <div className="h-[1em] overflow-hidden rounded-full bg-panel">
              <div className="h-full rounded-full bg-chalk transition-all duration-300" style={{ width: `${pct}%` }} />
            </div>
          </div>
        )}
        {reveal?.explanation && <p className="pop text-[2em] leading-snug text-paper">{reveal.explanation}</p>}
      </div>
    </div>
  );
}

// ------------------------------------------------------------------ Classifica
function BoardView({ game }: { game: GameStateDto }) {
  return (
    <div className="absolute inset-0 flex flex-col p-[3.2em]">
      <h2 className="mb-[1em] text-[4.4em] font-extrabold tracking-tight text-chalk">Classifica</h2>
      <Leaderboard entries={game.leaderboard ?? []} perColumn={10} />
    </div>
  );
}

function FinalView({ game }: { game: GameStateDto }) {
  const list = game.leaderboard ?? [];
  const top = list.slice(0, 3);
  // ordine podio: 2° - 1° - 3°
  const podium = [top[1], top[0], top[2]].filter(Boolean);
  const height: Record<number, string> = { 1: '19em', 2: '14em', 3: '10em' };
  return (
    <div className="absolute inset-0 flex flex-col p-[3em]">
      <h2 className="text-center text-[4.2em] font-extrabold tracking-tight text-chalk">Classifica finale</h2>
      <div className="mt-[1em] flex flex-1 items-end justify-center gap-[2em]">
        {podium.map((e) => (
          <div key={e.teamId} className="flex w-[22em] flex-col items-center">
            <p className="max-w-full truncate text-[2.8em] font-extrabold">{e.nickname}</p>
            <p className="mb-[0.4em] text-[2em] text-mute">{e.score} punti</p>
            <div className="flex w-full items-start justify-center rounded-t-[0.8em] bg-panel pt-[0.6em] text-[5em] font-extrabold text-chalk"
              style={{ height: height[Math.min(e.rank, 3)] }}>{e.rank}</div>
          </div>
        ))}
      </div>
      {list.length > 3 && (
        <div className="mt-[1.6em] scale-[0.9] origin-top">
          <Leaderboard entries={list} skip={3} perColumn={4} />
        </div>
      )}
    </div>
  );
}
