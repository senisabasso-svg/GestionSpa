import { useEffect, useState } from 'react';
import { api } from '../api/client';
import type { InformeResumen } from '../types';
import { formatUYU } from '../types';
import { Users, DollarSign, AlertTriangle, DoorOpen } from 'lucide-react';

export default function Dashboard() {
  const [resumen, setResumen] = useState<InformeResumen | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.informes.resumen()
      .then(setResumen)
      .catch(console.error)
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <div className="empty-state">Cargando panel...</div>;

  return (
    <div>
      <div className="page-header">
        <h2>Panel de Control</h2>
        <p>SPA Thermal Daymán — Termas del Daymán, Salto</p>
      </div>

      <div className="card-grid">
        <div className="stat-card success">
          <div className="label">Cobrado este mes</div>
          <div className="value">{formatUYU(resumen?.totalCobrado ?? 0)}</div>
        </div>
        <div className="stat-card warning">
          <div className="label">Pendiente de cobro</div>
          <div className="value">{formatUYU(resumen?.totalPendiente ?? 0)}</div>
        </div>
        <div className="stat-card">
          <Users size={20} style={{ marginBottom: 8, color: 'var(--color-primary)' }} />
          <div className="label">Socios activos</div>
          <div className="value">{resumen?.sociosActivos ?? 0}</div>
        </div>
        <div className="stat-card">
          <DoorOpen size={20} style={{ marginBottom: 8, color: 'var(--color-primary)' }} />
          <div className="label">Ingresos hoy</div>
          <div className="value">{resumen?.ingresosHoy ?? 0}</div>
        </div>
        <div className="stat-card danger">
          <AlertTriangle size={20} style={{ marginBottom: 8, color: 'var(--color-danger)' }} />
          <div className="label">Cuotas pendientes</div>
          <div className="value">{resumen?.cuotasPendientes ?? 0}</div>
        </div>
        <div className="stat-card warning">
          <DollarSign size={20} style={{ marginBottom: 8, color: 'var(--color-warning)' }} />
          <div className="label">Cargos clientes pendientes</div>
          <div className="value">{resumen?.cargosPendientes ?? 0}</div>
        </div>
      </div>

      <div className="card">
        <h3 style={{ marginBottom: '1rem', color: 'var(--color-primary-dark)' }}>Bienvenido al sistema de gestión</h3>
        <p style={{ color: 'var(--color-text-muted)', lineHeight: 1.7 }}>
          Este sistema permite administrar socios, clientes ocasionales, servicios del spa
          (masajes, tratamientos termales, fango facial, hidromasajes y más), cuotas mensuales,
          cobranzas e ingresos al complejo termal del Daymán en Salto, Uruguay.
        </p>
        <div style={{ marginTop: '1.5rem', display: 'flex', gap: '1rem', flexWrap: 'wrap' }}>
          <a href="/ingreso" className="btn btn-primary">Abrir Control de Ingreso</a>
          <a href="/informes" className="btn btn-secondary">Ver Informes</a>
        </div>
      </div>
    </div>
  );
}
