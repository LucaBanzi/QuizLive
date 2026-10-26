import * as signalR from '@microsoft/signalr';

export type ConnStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected';

export interface HubSessionOptions {
  accessToken?: () => string;
  handlers: Record<string, (...args: any[]) => void>;
  /** Chiamata dopo ogni (ri)connessione riuscita: l'id di connessione è nuovo e i gruppi sono persi. */
  onConnected: (conn: signalR.HubConnection) => Promise<void>;
  onStatus: (s: ConnStatus) => void;
  /** Chiamata quando la pagina torna in primo piano / torna la rete e la connessione è viva. */
  onResync?: () => void;
}

export interface HubSession {
  conn: signalR.HubConnection;
  start: () => Promise<void>;
  stop: () => Promise<void>;
}

const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));

export function createHubSession(o: HubSessionOptions): HubSession {
  const conn = new signalR.HubConnectionBuilder()
    .withUrl('/hub/quiz', o.accessToken ? { accessTokenFactory: o.accessToken } : {})
    .withAutomaticReconnect([0, 1000, 2000, 5000, 10000, 15000])
    .configureLogging(signalR.LogLevel.Warning)
    .build();
  conn.serverTimeoutInMilliseconds = 45_000;
  conn.keepAliveIntervalInMilliseconds = 15_000;

  for (const [name, fn] of Object.entries(o.handlers)) conn.on(name, fn);

  let stopped = false;
  let looping = false;

  // Dopo l'ultimo tentativo automatico: ciclo manuale start() ogni 5 s, senza mai arrendersi.
  const loop = async () => {
    if (looping) return;
    looping = true;
    try {
      while (!stopped) {
        try {
          o.onStatus('connecting');
          await conn.start();
          await o.onConnected(conn);
          o.onStatus('connected');
          return;
        } catch {
          o.onStatus('disconnected');
          try { if (conn.state !== signalR.HubConnectionState.Disconnected) await conn.stop(); } catch { /* ignore */ }
          await sleep(5000);
        }
      }
    } finally {
      looping = false;
    }
  };

  conn.onreconnecting(() => o.onStatus('reconnecting'));
  conn.onreconnected(async () => {
    try {
      await o.onConnected(conn);
      o.onStatus('connected');
    } catch {
      try { await conn.stop(); } catch { /* ignore */ }   // onclose → ciclo manuale
    }
  });
  conn.onclose(() => {
    if (stopped) return;
    o.onStatus('disconnected');
    void loop();
  });

  const wake = () => {
    if (stopped) return;
    if (conn.state === signalR.HubConnectionState.Connected) o.onResync?.();
    else if (conn.state === signalR.HubConnectionState.Disconnected) void loop();
  };
  const onVisible = () => { if (document.visibilityState === 'visible') wake(); };
  document.addEventListener('visibilitychange', onVisible);
  window.addEventListener('online', wake);

  return {
    conn,
    start: loop,
    stop: async () => {
      stopped = true;
      document.removeEventListener('visibilitychange', onVisible);
      window.removeEventListener('online', wake);
      try { await conn.stop(); } catch { /* ignore */ }
    },
  };
}
