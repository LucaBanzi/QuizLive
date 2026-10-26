import { useEffect, useRef, useState } from 'react';
import { api, downloadFile } from '../../api/http';
import type { Label, MediaType, QuestionAdminDto, QuestionEdit } from '../../api/types';
import { errMessage } from '../../lib/format';
import MediaView from '../../components/MediaView';

const LABELS: Label[] = ['A', 'B', 'C', 'D'];
const empty = (): QuestionEdit => ({
  text: '', mediaType: 'None', mediaUrl: null, correctLabel: 'A', explanation: '',
  options: LABELS.map((l) => ({ label: l, text: '' })),
});

export default function QuestionsPage() {
  const [items, setItems] = useState<QuestionAdminDto[]>([]);
  const [form, setForm] = useState<(QuestionEdit & { id?: string }) | null>(null);
  const [msg, setMsg] = useState<{ ok: boolean; text: string } | null>(null);
  const [uploading, setUploading] = useState(false);
  const dragFrom = useRef<number | null>(null);
  const importRef = useRef<HTMLInputElement>(null);
  const [importMode, setImportMode] = useState<'append' | 'replace'>('append');

  const load = () => api<QuestionAdminDto[]>('/api/admin/questions').then(setItems).catch((e) => fail(e));
  useEffect(() => { void load(); }, []);

  const ok = (text: string) => setMsg({ ok: true, text });
  const fail = (e: unknown) => {
    const code = (e as { code?: string })?.code;
    const known = code ? errMessage(code) : null;
    const text = known && known !== 'Errore imprevisto.' ? known : code || (e as Error)?.message || 'Errore imprevisto.';
    setMsg({ ok: false, text });
  };

  async function save() {
    if (!form) return;
    const body = { ...form, explanation: form.explanation?.trim() || null, id: undefined };
    try {
      if (form.id) await api(`/api/admin/questions/${form.id}`, { method: 'PUT', json: body });
      else await api('/api/admin/questions', { json: body });
      setForm(null); ok('Domanda salvata.'); await load();
    } catch (e) { fail(e); }
  }

  async function remove(q: QuestionAdminDto) {
    if (!window.confirm(`Eliminare la domanda "${q.text.slice(0, 40)}"?`)) return;
    try { await api(`/api/admin/questions/${q.id}`, { method: 'DELETE' }); ok('Domanda eliminata.'); await load(); } catch (e) { fail(e); }
  }

  async function onDrop(to: number) {
    const from = dragFrom.current; dragFrom.current = null;
    if (from === null || from === to) return;
    const next = [...items];
    const [moved] = next.splice(from, 1);
    next.splice(to, 0, moved);
    setItems(next);
    try { await api('/api/admin/questions/reorder', { method: 'PUT', json: { orderedIds: next.map((x) => x.id) } }); await load(); }
    catch (e) { fail(e); await load(); }
  }

  async function upload(file: File) {
    setUploading(true);
    try {
      const fd = new FormData(); fd.append('file', file);
      const r = await api<{ url: string; mediaType: MediaType }>('/api/admin/media', { body: fd });
      setForm((f) => (f ? { ...f, mediaUrl: r.url, mediaType: r.mediaType } : f));
    } catch (e) { fail(e); } finally { setUploading(false); }
  }

  async function importFile(file: File) {
    try {
      const data = JSON.parse(await file.text());
      if (importMode === 'replace' && !window.confirm('Sostituire TUTTE le domande esistenti?')) return;
      const r = await api<{ imported: number }>(`/api/admin/questions/import?mode=${importMode}`, { json: data });
      ok(`Importate ${r.imported} domande.`); await load();
    } catch (e) { fail(e); } finally { if (importRef.current) importRef.current.value = ''; }
  }

  async function resetQuiz() {
    if (!window.confirm('RESET SERATA: cancella tavoli, risposte e correzioni e torna alla Lobby. Continuare?')) return;
    const word = window.prompt('Conferma finale: scrivi RESET');
    if (word?.trim().toUpperCase() !== 'RESET') return;
    try { await api('/api/admin/quiz/reset?confirm=true', { method: 'POST' }); ok('Serata azzerata.'); } catch (e) { fail(e); }
  }

  const input = 'mt-1 w-full rounded-lg border border-line bg-ink px-3 py-2';

  return (
    <main className="mx-auto max-w-4xl space-y-5 p-4">
      <header className="flex flex-wrap items-center gap-2">
        <h1 className="mr-auto text-2xl font-extrabold">Domande ({items.length})</h1>
        <button className="rounded-lg bg-chalk px-3 py-2 font-bold text-ink" onClick={() => setForm(empty())}>+ Nuova domanda</button>
        <select value={importMode} onChange={(e) => setImportMode(e.target.value as 'append' | 'replace')}
          className="rounded-lg border border-line bg-panel px-2 py-2 text-sm">
          <option value="append">Importa: accoda</option>
          <option value="replace">Importa: sostituisci</option>
        </select>
        <button className="rounded-lg border border-line px-3 py-2 text-sm" onClick={() => importRef.current?.click()}>Importa JSON</button>
        <input ref={importRef} type="file" accept="application/json,.json" hidden onChange={(e) => e.target.files?.[0] && void importFile(e.target.files[0])} />
        <button className="rounded-lg border border-line px-3 py-2 text-sm" onClick={() => void downloadFile('/api/admin/export/results.csv', 'risultati.csv').catch(fail)}>Esporta CSV</button>
        <button className="rounded-lg border border-bad/60 px-3 py-2 text-sm text-bad" onClick={() => void resetQuiz()}>Reset serata</button>
      </header>

      {msg && <p role="status" className={`rounded-xl border p-3 ${msg.ok ? 'border-ok/50 bg-ok/10 text-ok' : 'border-bad/50 bg-bad/10 text-bad'}`}>{msg.text}</p>}

      <ul className="space-y-2">
        {items.length === 0 && <li className="rounded-xl border border-dashed border-line p-6 text-center text-mute">Nessuna domanda. Creane una o importa un file JSON.</li>}
        {items.map((q, i) => (
          <li key={q.id} draggable onDragStart={() => { dragFrom.current = i; }} onDragOver={(e) => e.preventDefault()} onDrop={() => void onDrop(i)}
            className="flex cursor-grab items-center gap-3 rounded-xl border border-line bg-panel p-3">
            <span className="select-none text-mute" title="Trascina per riordinare">⠿</span>
            <span className="w-7 text-center font-extrabold text-chalk">{q.orderIndex}</span>
            <div className="min-w-0 flex-1">
              <p className="truncate font-semibold">{q.text}</p>
              <p className="text-xs text-mute">Corretta: {q.correctLabel}{q.mediaType !== 'None' && ` · ${q.mediaType}`}</p>
            </div>
            <button className="rounded-lg border border-line px-3 py-1.5 text-sm" onClick={() => setForm({ ...q })}>Modifica</button>
            <button className="rounded-lg border border-line px-3 py-1.5 text-sm text-bad" onClick={() => void remove(q)}>Elimina</button>
          </li>
        ))}
      </ul>

      {form && (
        <div className="fixed inset-0 z-50 overflow-y-auto bg-black/70 p-4" role="dialog" aria-modal>
          <div className="mx-auto my-6 w-full max-w-2xl rounded-2xl border border-line bg-panel p-5">
            <h2 className="text-lg font-bold">{form.id ? 'Modifica domanda' : 'Nuova domanda'}</h2>

            <label className="mt-4 block text-sm text-mute">Testo della domanda
              <textarea className={input} rows={3} maxLength={500} value={form.text} onChange={(e) => setForm({ ...form, text: e.target.value })} />
            </label>

            <div className="mt-4 space-y-2">
              {form.options.map((o, i) => (
                <div key={o.label} className="flex items-center gap-2">
                  <label className="flex items-center gap-2" title="Risposta corretta">
                    <input type="radio" name="correct" checked={form.correctLabel === o.label} onChange={() => setForm({ ...form, correctLabel: o.label })} />
                    <b className="w-5">{o.label}</b>
                  </label>
                  <input className={`${input} mt-0`} maxLength={200} value={o.text} placeholder={`Opzione ${o.label}`}
                    onChange={(e) => setForm({ ...form, options: form.options.map((x, k) => (k === i ? { ...x, text: e.target.value } : x)) })} />
                </div>
              ))}
              <p className="text-xs text-mute">Il pallino indica la risposta corretta.</p>
            </div>

            <label className="mt-4 block text-sm text-mute">Spiegazione (opzionale, mostrata dopo la risposta)
              <input className={input} maxLength={500} value={form.explanation ?? ''} onChange={(e) => setForm({ ...form, explanation: e.target.value })} />
            </label>

            <div className="mt-4">
              <p className="text-sm text-mute">Media (immagine, audio o video — max 100 MB)</p>
              <div className="mt-1 flex items-center gap-2">
                <input type="file" accept=".jpg,.jpeg,.png,.webp,.gif,.mp3,.ogg,.wav,.mp4,.webm" disabled={uploading}
                  onChange={(e) => e.target.files?.[0] && void upload(e.target.files[0])} />
                {uploading && <span className="text-sm text-mute">Caricamento…</span>}
                {form.mediaUrl && <button className="text-sm text-bad underline" onClick={() => setForm({ ...form, mediaUrl: null, mediaType: 'None' })}>Rimuovi</button>}
              </div>
              {form.mediaUrl && <div className="mt-2 h-44 overflow-hidden rounded-lg bg-black/30"><MediaView type={form.mediaType} url={form.mediaUrl} controls /></div>}
            </div>

            <div className="mt-6 flex justify-end gap-2">
              <button className="rounded-lg px-4 py-2 text-mute" onClick={() => setForm(null)}>Annulla</button>
              <button className="rounded-lg bg-chalk px-4 py-2 font-bold text-ink" onClick={() => void save()}>Salva</button>
            </div>
          </div>
        </div>
      )}
    </main>
  );
}
