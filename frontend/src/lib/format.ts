export const LABELS = ['A', 'B', 'C', 'D'] as const;

const ERR: Record<string, string> = {
  NICKNAME_TAKEN: 'Questo nome è già in uso.',
  TABLE_TAKEN: 'Questo numero di tavolo è già occupato.',
  VALIDATION: 'Controlla i dati inseriti.',
  TEAM_LIMIT: 'Numero massimo di tavoli raggiunto.',
  RATE_LIMIT: 'Troppi tentativi. Riprova tra un minuto.',
  NOT_AUTHORIZED: 'Tavolo non trovato o non attivo.',
  RECOVERY_CLOSED: 'Il recupero non è aperto per questo tavolo. Chiedi al presentatore di sbloccarlo.',
  NOT_REGISTERED: 'Non sei registrato.',
  INVALID_TRANSITION: 'Azione non consentita in questa fase.',
  INVALID_PHASE: 'Azione non consentita in questa fase.',
  NO_QUESTIONS: 'Non ci sono domande: aggiungile in "Domande".',
  TEAM_NOT_FOUND: 'Tavolo non trovato.',
  QUESTION_NOT_FOUND: 'Domanda non trovata.',
  QUESTION_IN_USE: 'La domanda è in uso: non si può modificare in questa fase.',
};

export function errCode(e: unknown): string {
  const msg = e instanceof Error ? e.message : String(e);
  return Object.keys(ERR).find((k) => msg.includes(k)) ?? 'UNKNOWN';
}
export function errText(e: unknown): string {
  return ERR[errCode(e)] ?? 'Errore imprevisto. Riprova.';
}
export function errMessage(code: string): string { return ERR[code] ?? 'Errore imprevisto.'; }

/** crypto.randomUUID non esiste su http (LAN): fallback. */
export function uuid(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) return crypto.randomUUID();
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    return (c === 'x' ? r : (r & 0x3) | 0x8).toString(16);
  });
}

export const INVALIDATION_TEXT: Record<string, string> = {
  RESET: 'Il presentatore ha sbloccato il tavolo. Per riprendere usa "Riprendi un tavolo esistente".',
  REPLACED: 'Il tavolo è stato ripreso da un altro dispositivo.',
  DEACTIVATED: 'Il tavolo è stato disattivato dal presentatore.',
  TRANSFERRED: 'Il tavolo è stato trasferito su un altro dispositivo.',
};
