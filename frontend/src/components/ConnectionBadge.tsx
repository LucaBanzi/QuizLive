import type { ConnStatus } from '../realtime/hubClient';

const MAP: Record<ConnStatus, { dot: string; text: string }> = {
  connected: { dot: 'bg-ok', text: 'Connesso' },
  connecting: { dot: 'bg-ansC animate-pulse', text: 'Connessione…' },
  reconnecting: { dot: 'bg-ansC animate-pulse', text: 'Riconnessione…' },
  disconnected: { dot: 'bg-bad', text: 'Offline' },
};

export default function ConnectionBadge({ status, compact = false }: { status: ConnStatus; compact?: boolean }) {
  const m = MAP[status];
  return (
    <span className="inline-flex items-center gap-2 text-xs text-mute" role="status" aria-live="polite">
      <span className={`inline-block h-2.5 w-2.5 rounded-full ${m.dot}`} />
      {!compact && m.text}
    </span>
  );
}
