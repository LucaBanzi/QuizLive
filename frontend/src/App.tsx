import { Navigate, Route, Routes } from 'react-router-dom';
import GamePage from './pages/player/GamePage';
import RecoverPage from './pages/player/RecoverPage';
import DisplayPage from './pages/display/DisplayPage';
import LoginPage from './pages/admin/LoginPage';
import AdminLayout from './pages/admin/AdminLayout';
import DashboardPage from './pages/admin/DashboardPage';
import QuestionsPage from './pages/admin/QuestionsPage';

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<GamePage />} />
      <Route path="/recover" element={<RecoverPage />} />
      <Route path="/display" element={<DisplayPage />} />
      <Route path="/admin/login" element={<LoginPage />} />
      <Route path="/admin" element={<AdminLayout />}>
        <Route index element={<DashboardPage />} />
        <Route path="questions" element={<QuestionsPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
