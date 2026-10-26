import type { Label } from '../api/types';

export type BtnState = 'idle' | 'disabled' | 'selected' | 'sending' | 'correct' | 'wrong' | 'dim';

const BG: Record<Label, string> = { A: 'bg-ansA text-white', B: 'bg-ansB text-white', C: 'bg-ansC text-ink', D: 'bg-ansD text-white' };
// la forma si aggiunge al colore: non ci si affida al solo colore
export const SHAPE: Record<Label, string> = { A: '▲', B: '◆', C: '●', D: '■' };

const STATE: Record<BtnState, string> = {
  idle: 'active:scale-95',
  disabled: 'opacity-45',
  selected: 'ring-8 ring-white',
  sending: 'ring-8 ring-white animate-pulse',
  correct: 'ring-8 ring-ok',
  wrong: 'ring-8 ring-bad opacity-80',
  dim: 'opacity-30',
};

interface Props { label: Label; state: BtnState; onClick?: () => void }

export default function AnswerButton({ label, state, onClick }: Props) {
  const clickable = state === 'idle';
  return (
    <button
      type="button"
      disabled={!clickable}
      onClick={onClick}
      aria-label={`Risposta ${label}`}
      aria-pressed={state === 'selected' || state === 'sending'}
      className={`relative flex flex-col items-center justify-center rounded-3xl font-extrabold shadow-lg transition ${BG[label]} ${STATE[state]}`}
    >
      <span className="[font-size:min(22vw,22vh)] leading-none">{label}</span>
      <span className="text-3xl leading-none opacity-80" aria-hidden>{SHAPE[label]}</span>
      {state === 'correct' && <span className="absolute right-3 top-3 text-3xl" aria-hidden>✓</span>}
      {state === 'wrong' && <span className="absolute right-3 top-3 text-3xl" aria-hidden>✗</span>}
    </button>
  );
}
