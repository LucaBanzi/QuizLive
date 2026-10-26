import { Link, Navigate, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { clearAdminToken, getAdminToken } from '../../lib/storage';

/** Protegge /admin e /admin/questions: senza token valido si torna al login. */
export default function AdminLayout() {
  const navigate = useNavigate();
  const { pathname } = useLocation();
  if (!getAdminToken()) return <Navigate to="/admin/login" replace />;

  const tab = (to: string, label: string) => (
    <Link to={to} className={`rounded-lg px-3 py-1.5 text-sm font-semibold ${pathname === to ? 'bg-chalk text-ink' : 'text-mute hover:text-paper'}`}>{label}</Link>
  );
  return (
    <div className="min-h-screen bg-ink text-paper">
      <nav className="sticky top-0 z-40 flex items-center gap-2 border-b border-line bg-ink/95 px-4 py-3 backdrop-blur">
        <strong className="mr-3 text-chalk">QuizLive</strong>
        {tab('/admin', 'Regia')}
        {tab('/admin/questions', 'Domande')}
        <a href="/display" target="_blank" rel="noreferrer" className="ml-auto text-sm text-mute underline">Apri Display</a>
        <button className="rounded-lg px-3 py-1.5 text-sm text-mute hover:text-paper"
          onClick={() => { clearAdminToken(); navigate('/admin/login'); }}>Esci</button>
      </nav>
      <Outlet />
    </div>
  );
}
