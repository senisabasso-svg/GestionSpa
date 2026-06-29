import { useState, useEffect } from 'react';
import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import {
  LayoutDashboard, Users, UserPlus, Sparkles, Receipt,
  BarChart3, DoorOpen, CreditCard, Menu, ChevronsLeft, Home, Building2, LogOut
} from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import { APP_NAME } from '../config/branding';

const MOBILE_QUERY = '(max-width: 768px)';

export default function Layout() {
  const { isSuperAdmin, emisorNombre, emisorSlug, nombre, logout, activeEmisorId } = useAuth();
  const navigate = useNavigate();
  const [sidebarOpen, setSidebarOpen] = useState(true);
  const [isMobile, setIsMobile] = useState(false);

  const brandName = emisorNombre || APP_NAME;
  const brandSub = isSuperAdmin && !activeEmisorId ? 'Seleccioná un emisor' : (emisorSlug ? `/${emisorSlug}` : 'Panel de gestión');

  const navItems = [
    { to: '/', icon: LayoutDashboard, label: 'Panel' },
    ...(isSuperAdmin ? [{ to: '/emisores', icon: Building2, label: 'Emisores' }] : []),
    { to: '/socios', icon: Users, label: 'Socios' },
    { to: '/familias', icon: Home, label: 'Familia' },
    { to: '/clientes', icon: UserPlus, label: 'Clientes' },
    { to: '/servicios', icon: Sparkles, label: 'Servicios' },
    { to: '/cargos', icon: Receipt, label: 'Cargos' },
    { to: '/cuotas', icon: CreditCard, label: 'Cuotas' },
    { to: '/informes', icon: BarChart3, label: 'Informes' },
  ];

  useEffect(() => {
    const mq = window.matchMedia(MOBILE_QUERY);
    const sync = (mobile: boolean) => {
      setIsMobile(mobile);
      setSidebarOpen(!mobile);
    };
    sync(mq.matches);
    const onChange = (e: MediaQueryListEvent) => sync(e.matches);
    mq.addEventListener('change', onChange);
    return () => mq.removeEventListener('change', onChange);
  }, []);

  useEffect(() => {
    document.body.style.overflow = isMobile && sidebarOpen ? 'hidden' : '';
    return () => { document.body.style.overflow = ''; };
  }, [isMobile, sidebarOpen]);

  const closeSidebar = () => setSidebarOpen(false);
  const openSidebar = () => setSidebarOpen(true);

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const ingresoUrl = emisorSlug ? `/ingreso?emisor=${emisorSlug}` : '/ingreso';

  return (
    <div className={`app-layout${sidebarOpen ? '' : ' sidebar-collapsed'}${isMobile ? ' is-mobile' : ''}`}>
      {isMobile && sidebarOpen && (
        <button type="button" className="sidebar-backdrop" onClick={closeSidebar} aria-label="Cerrar menú" />
      )}

      <aside className="sidebar" aria-hidden={!sidebarOpen}>
        <button type="button" className="sidebar-toggle sidebar-toggle--collapse" onClick={closeSidebar} aria-label="Ocultar menú" title="Ocultar menú">
          <ChevronsLeft size={18} />
        </button>
        <div className="sidebar-brand">
          <h1>{brandName}</h1>
          <p>{brandSub}</p>
        </div>
        <nav className="sidebar-nav">
          {navItems.map(({ to, icon: Icon, label }) => (
            <NavLink key={to} to={to} end={to === '/'} className={({ isActive }) => `nav-link${isActive ? ' active' : ''}`} onClick={() => { if (isMobile) closeSidebar(); }}>
              <Icon size={18} />
              {label}
            </NavLink>
          ))}
          <a href={ingresoUrl} className="nav-link" onClick={() => { if (isMobile) closeSidebar(); }}>
            <DoorOpen size={18} />
            Control de Ingreso
          </a>
        </nav>
        <div className="sidebar-footer">
          <div className="sidebar-user">{nombre}</div>
          <button type="button" className="btn btn-sm btn-secondary sidebar-logout" onClick={handleLogout}>
            <LogOut size={14} /> Salir
          </button>
        </div>
      </aside>

      <div className="main-column">
        <header className="top-bar">
          <button type="button" className="sidebar-toggle sidebar-toggle--expand" onClick={openSidebar} aria-label="Mostrar menú" title="Mostrar menú">
            <Menu size={20} />
          </button>
          <span className="top-bar-brand">{brandName}</span>
        </header>
        <main className="main-content">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
