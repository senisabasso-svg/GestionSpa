import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import ProtectedRoute from './components/ProtectedRoute';
import Layout from './components/Layout';
import LoginPage from './pages/LoginPage';
import Dashboard from './pages/Dashboard';
import SociosPage from './pages/SociosPage';
import FamiliasPage from './pages/FamiliasPage';
import EmisoresPage from './pages/EmisoresPage';
import ClientesPage from './pages/ClientesPage';
import ServiciosPage from './pages/ServiciosPage';
import CargosPage from './pages/CargosPage';
import CuotasPage from './pages/CuotasPage';
import InformesPage from './pages/InformesPage';
import IngresoPage from './pages/IngresoPage';
import NotFoundPage from './pages/NotFoundPage';

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/ingreso" element={<IngresoPage />} />
          <Route element={<ProtectedRoute><Layout /></ProtectedRoute>}>
            <Route path="/" element={<Dashboard />} />
            <Route path="/emisores" element={<EmisoresPage />} />
            <Route path="/socios" element={<SociosPage />} />
            <Route path="/familias" element={<FamiliasPage />} />
            <Route path="/clientes" element={<ClientesPage />} />
            <Route path="/servicios" element={<ServiciosPage />} />
            <Route path="/cargos" element={<CargosPage />} />
            <Route path="/cuotas" element={<CuotasPage />} />
            <Route path="/informes" element={<InformesPage />} />
          </Route>
          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}
