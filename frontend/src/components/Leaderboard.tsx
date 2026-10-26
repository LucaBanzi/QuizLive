import type { LeaderboardEntryDto } from '../api/types';

interface Props { entries: LeaderboardEntryDto[]; perColumn?: number; skip?: number }

const ROW = 5.4; // em (relativi allo stage)

/** Classifica animata: le righe scorrono nella nuova posizione. Fino a 2 colonne. */
export default function Leaderboard({ entries, perColumn = 10, skip = 0 }: Props) {
  const list = entries.slice(skip, skip + perColumn * 2);
  const twoCols = list.length > perColumn;
  const rows = Math.min(perColumn, list.length);

  return (
    <div className="relative w-full" style={{ height: `${rows * ROW}em` }}>
      {list.map((e, i) => {
        const col = Math.floor(i / perColumn);
        const row = i % perColumn;
        return (
          <div
            key={e.teamId}
            className="absolute flex items-center gap-[1.2em] rounded-[0.8em] bg-panel px-[1.4em] transition-all duration-700 ease-out"
            style={{
              top: `${row * ROW}em`,
              left: twoCols ? `${col * 51}%` : 0,
              width: twoCols ? '49%' : '100%',
              height: `${ROW - 0.9}em`,
            }}
          >
            <span className="w-[2.2em] text-center text-[2.6em] font-extrabold text-chalk">{e.rank}</span>
            <span className="min-w-0 flex-1 truncate text-[2.4em] font-bold">{e.nickname}</span>
            <span className="text-[1.6em] text-mute">Tav. {e.tableNumber}</span>
            {e.lastQuestionPoints > 0 && <span className="text-[1.8em] font-bold text-ok">+{e.lastQuestionPoints}</span>}
            <span className="w-[3.4em] text-right text-[2.6em] font-extrabold">{e.score}</span>
          </div>
        );
      })}
    </div>
  );
}
