import { useCallback, useEffect, useRef } from 'react';
import { createHubSession, type HubSession } from './hubClient';
import { usePlayerStore } from '../store/playerStore';
import { useDisplayStore } from '../store/displayStore';
import { useAdminStore } from '../store/adminStore';
import { clearSession, getAdminToken, loadSession } from '../lib/storage';
import { errCode, errText } from '../lib/format';
import type {
  AdminStateDto, GameStateDto, LeaderboardEntryDto, PlayerStateDto, RejoinResult,
} from '../api/types';

/** Hub lato giocatore. Con autoRejoin ripristina la sessione salvata prima di mostrare qualsiasi form. */
export function usePlayerHub(autoRejoin: boolean) {
  const ref = useRef<HubSession | null>(null);

  useEffect(() => {
    const st = usePlayerStore.getState;
    const sess: HubSession = createHubSession({
      handlers: {
        playerStateUpdated: (s: PlayerStateDto) => st().applyState(s),
        sessionInvalidated: (reason: string) => { clearSession(); st().invalidate(reason); },
      },
      onStatus: (s) => st().setStatus(s),
      onConnected: async (conn) => {
        if (!autoRejoin) return;
        const saved = loadSession();
        if (!saved) { st().setBoot('needsRegister'); return; }
        try {
          // dopo ogni riconnessione il connectionId è nuovo: si rifà sempre Rejoin
          const r = await conn.invoke<RejoinResult>('Rejoin', { nickname: saved.nickname, sessionToken: saved.sessionToken });
          st().login(r);
        } catch (e) {
          const code = errCode(e);
          if (code === 'NOT_AUTHORIZED' || code === 'RECOVERY_CLOSED') {
            clearSession();
            st().setSession(null);
            st().setBoot('needsRegister');
          } else throw e;     // errore di rete: il ciclo di riconnessione riprova
        }
      },
      onResync: async () => {
        if (!autoRejoin || st().boot !== 'ready') return;
        try { st().applyState(await sess.conn.invoke<PlayerStateDto>('GetMyState')); } catch { /* ignore */ }
      },
    });
    ref.current = sess;
    void sess.start();
    return () => { void sess.stop(); ref.current = null; };
  }, [autoRejoin]);

  return ref;
}

export function useDisplayHub() {
  useEffect(() => {
    const st = useDisplayStore.getState;
    const sess: HubSession = createHubSession({
      handlers: {
        displayStateUpdated: (g: GameStateDto) => st().setGame(g),
        answerCountUpdated: (a: number, t: number) => st().setCount(a, t),
        leaderboardUpdated: (l: LeaderboardEntryDto[]) => st().setLeaderboard(l),
      },
      onStatus: (s) => st().setStatus(s),
      onConnected: async (conn) => { st().setGame(await conn.invoke<GameStateDto>('JoinDisplay')); },
      onResync: async () => { try { st().setGame(await sess.conn.invoke<GameStateDto>('JoinDisplay')); } catch { /* ignore */ } },
    });
    void sess.start();

    fetch('/api/public/info').then((r) => r.json()).then((i) => st().setOrigin(i.publicOrigin ?? '')).catch(() => {});
    return () => { void sess.stop(); };
  }, []);
}

export function useAdminHub() {
  const ref = useRef<HubSession | null>(null);

  useEffect(() => {
    const st = useAdminStore.getState;
    const sess: HubSession = createHubSession({
      accessToken: () => getAdminToken() ?? '',
      handlers: {
        adminStateUpdated: (s: AdminStateDto) => st().applyState(s),
        answerCountUpdated: (a: number, t: number) => st().setCount(a, t),
        leaderboardUpdated: () => { /* già incluso in AdminStateDto */ },
      },
      onStatus: (s) => st().setStatus(s),
      onConnected: async (conn) => { st().applyState(await conn.invoke<AdminStateDto>('JoinAdmin'), true); },
      onResync: async () => { try { st().applyState(await sess.conn.invoke<AdminStateDto>('JoinAdmin'), true); } catch { /* ignore */ } },
    });
    ref.current = sess;
    void sess.start();
    return () => { void sess.stop(); ref.current = null; };
  }, []);

  /** Esegue un comando admin; l'esito (stato aggiornato o errore) finisce nello store. */
  const invoke = useCallback(async (method: string, ...args: unknown[]) => {
    const st = useAdminStore.getState();
    st.setBusy(true);
    try {
      const s = await ref.current!.conn.invoke<AdminStateDto>(method, ...args);
      st.applyState(s);
      st.setError(null);
      return s;
    } catch (e) {
      st.setError(errText(e));
      return null;
    } finally {
      useAdminStore.getState().setBusy(false);
    }
  }, []);

  return { invoke };
}
