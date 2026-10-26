import { create } from 'zustand';
import type { ConnStatus } from '../realtime/hubClient';
import type { Label, PlayerStateDto } from '../api/types';
import { saveSession, type StoredSession } from '../lib/storage';
import { INVALIDATION_TEXT } from '../lib/format';

type Boot = 'booting' | 'needsRegister' | 'ready';

interface PlayerStore {
  status: ConnStatus;
  boot: Boot;
  session: StoredSession | null;
  state: PlayerStateDto | null;
  /** punteggio visto prima del reveal, per mostrare "+100" */
  baseScore: number;
  pending: Label | null;
  notice: string | null;
  setStatus: (s: ConnStatus) => void;
  setBoot: (b: Boot) => void;
  setSession: (s: StoredSession | null) => void;
  login: (r: { teamId: string; sessionToken: string; state: PlayerStateDto }) => void;
  applyState: (s: PlayerStateDto) => void;
  invalidate: (reason: string) => void;
  setPending: (l: Label | null) => void;
  setNotice: (n: string | null) => void;
}

export const usePlayerStore = create<PlayerStore>((set, get) => ({
  status: 'connecting',
  boot: 'booting',
  session: null,
  state: null,
  baseScore: 0,
  pending: null,
  notice: null,

  setStatus: (status) => set({ status }),
  setBoot: (boot) => set({ boot }),
  setSession: (session) => set({ session }),

  login: (r) => {
    const session: StoredSession = { teamId: r.teamId, nickname: r.state.nickname, sessionToken: r.sessionToken, savedAt: Date.now() };
    saveSession(session);
    set({ session, state: r.state, baseScore: r.state.score, boot: 'ready', pending: null, notice: null });
  },

  applyState: (s) => {
    const cur = get().state;
    if (cur && s.seq < cur.seq) return;                         // snapshot vecchio: scartato
    set({
      state: s,
      baseScore: s.phase === 'AnswerRevealed' ? get().baseScore : s.score,
      pending: s.myAnswer ? null : get().pending,
    });
  },

  invalidate: (reason) =>
    set({ session: null, state: null, boot: 'needsRegister', pending: null, notice: INVALIDATION_TEXT[reason] ?? null }),

  setPending: (pending) => set({ pending }),
  setNotice: (notice) => set({ notice }),
}));
