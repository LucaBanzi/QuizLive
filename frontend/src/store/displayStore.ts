import { create } from 'zustand';
import type { ConnStatus } from '../realtime/hubClient';
import type { GameStateDto, LeaderboardEntryDto } from '../api/types';

interface DisplayStore {
  status: ConnStatus;
  game: GameStateDto | null;
  answered: number;
  total: number;
  publicOrigin: string;
  setStatus: (s: ConnStatus) => void;
  setGame: (g: GameStateDto) => void;
  setCount: (answered: number, total: number) => void;
  setLeaderboard: (l: LeaderboardEntryDto[]) => void;
  setOrigin: (o: string) => void;
}

export const useDisplayStore = create<DisplayStore>((set, get) => ({
  status: 'connecting',
  game: null,
  answered: 0,
  total: 0,
  publicOrigin: '',
  setStatus: (status) => set({ status }),
  setGame: (g) => {
    const cur = get().game;
    if (cur && g.seq < cur.seq) return;
    set({ game: g, answered: g.answeredCount, total: g.activeTeams });
  },
  setCount: (answered, total) => set({ answered, total }),
  setLeaderboard: (l) => {
    const g = get().game;
    if (g && g.leaderboard !== null) set({ game: { ...g, leaderboard: l } });
  },
  setOrigin: (publicOrigin) => set({ publicOrigin }),
}));
