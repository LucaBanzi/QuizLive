import { create } from 'zustand';
import type { ConnStatus } from '../realtime/hubClient';
import type { AdminStateDto } from '../api/types';

interface AdminStore {
  status: ConnStatus;
  state: AdminStateDto | null;
  error: string | null;
  busy: boolean;
  setStatus: (s: ConnStatus) => void;
  applyState: (s: AdminStateDto, force?: boolean) => void;
  setCount: (answered: number, total: number) => void;
  setError: (e: string | null) => void;
  setBusy: (b: boolean) => void;
}

export const useAdminStore = create<AdminStore>((set, get) => ({
  status: 'connecting',
  state: null,
  error: null,
  busy: false,
  setStatus: (status) => set({ status }),
  applyState: (s, force) => {
    const cur = get().state;
    if (!force && cur && s.game.seq < cur.game.seq) return;
    set({ state: s });
  },
  setCount: (answered, total) => {
    const cur = get().state;
    if (cur) set({ state: { ...cur, game: { ...cur.game, answeredCount: answered, activeTeams: total } } });
  },
  setError: (error) => set({ error }),
  setBusy: (busy) => set({ busy }),
}));
