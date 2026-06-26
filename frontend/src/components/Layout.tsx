import { NavLink, Outlet } from 'react-router-dom';
import {
  LayoutDashboard, Users, UserPlus, Sparkles, Receipt,
  BarChart3, DoorOpen, CreditCard
} from 'lucide-react';

const navItems = [
  { to: '/', icon: LayoutDashboard, label: 'Panel' },
  { to: '/socios', icon: Users, label: 'Socios' },
  { to: '/clientes', icon: UserPlus, label: 'Clientes' },
  { to: '/servicios', icon: Sparkles, label: 'Servicios' },
  { to: '/cargos', icon: Receipt, label: 'Cargos' },
  { to: '/cuotas', icon: CreditCard, label: 'Cuotas' },
  { to: '/informes', icon: BarChart3, label: 'Informes' },
  { to: '/ingreso', icon: DoorOpen, label: 'Control de Ingreso' },
];

export default function Layout() {
  return (
    <div className="app-layout">
      <aside className="sidebar">
        <div className="sidebar-brand">
          <img src="/logo-spa.png" alt="SPA Thermal Daymán" className="brand-logo" />
          <p>Termas del Daymán · Salto, Uruguay</p>
        </div>
        <nav className="sidebar-nav">
          {navItems.map(({ to, icon: Icon, label }) => (
            <NavLink key={to} to={to} end={to === '/'} className={({ isActive }) => `nav-link${isActive ? ' active' : ''}`}>
              <Icon size={18} />
              {label}
            </NavLink>
          ))}
        </nav>
      </aside>
      <main className="main-content">
        <Outlet />
      </main>
    </div>
  );
}
