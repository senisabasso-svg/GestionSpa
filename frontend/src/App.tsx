import { BrowserRouter, Routes, Route } from 'react-router-dom';
import Layout from './components/Layout';
import Dashboard from './pages/Dashboard';
import SociosPage from './pages/SociosPage';
import ClientesPage from './pages/ClientesPage';
import ServiciosPage from './pages/ServiciosPage';
import CargosPage from './pages/CargosPage';
import CuotasPage from './pages/CuotasPage';
import InformesPage from './pages/InformesPage';
import IngresoPage from './pages/IngresoPage';

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/ingreso" element={<IngresoPage />} />
        <Route element={<Layout />}>
          <Route path="/" element={<Dashboard />} />
          <Route path="/socios" element={<SociosPage />} />
          <Route path="/clientes" element={<ClientesPage />} />
          <Route path="/servicios" element={<ServiciosPage />} />
          <Route path="/cargos" element={<CargosPage />} />
          <Route path="/cuotas" element={<CuotasPage />} />
          <Route path="/informes" element={<InformesPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}
