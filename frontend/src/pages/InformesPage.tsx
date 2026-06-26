import { useEffect, useState } from 'react';
import { api } from '../api/client';
import type { InformeResumen, InformeCobranza, Ingreso, EstadoPago } from '../types';
import { MESES, formatUYU, formatHora } from '../types';

const pagoBadge = (estado: EstadoPago) => {
  const map: Record<EstadoPago, string> = { Pagado: 'badge-success', Pendiente: 'badge-warning', Parcial: 'badge-info' };
  return <span className={`badge ${map[estado]}`}>{estado}</span>;
};

export default function InformesPage() {
  const [tab, setTab] = useState<'resumen' | 'cobranza' | 'ingresos' | 'servicios'>('resumen');
  const [mes, setMes] = useState(new Date().getMonth() + 1);
  const [anio, setAnio] = useState(new Date().getFullYear());
  const [resumen, setResumen] = useState<InformeResumen | null>(null);
  const [cobranza, setCobranza] = useState<InformeCobranza[]>([]);
  const [ingresos, setIngresos] = useState<{ fecha: string; totalEntradas: number; accesosPermitidos: number; accesosRechazados: number; detalle: Ingreso[] } | null>(null);
  const [ingresosLoading, setIngresosLoading] = useState(false);
  const [serviciosTop, setServiciosTop] = useState<{ nombre: string; cantidad: number; total: number }[]>([]);
  const [fechaIngresos, setFechaIngresos] = useState(new Date().toISOString().split('T')[0]);

  useEffect(() => {
    if (tab === 'resumen') api.informes.resumen(mes, anio).then(setResumen);
    if (tab === 'cobranza') api.informes.cobranza(mes, anio).then(setCobranza);
    if (tab === 'ingresos') {
      setIngresosLoading(true);
      api.informes.ingresosDiarios(fechaIngresos).then(setIngresos).finally(() => setIngresosLoading(false));
    }
    if (tab === 'servicios') api.informes.serviciosMasVendidos(mes, anio).then(setServiciosTop);
  }, [tab, mes, anio, fechaIngresos]);

  return (
    <div>
      <div className="page-header">
        <h2>Informes y Control de Cobranza</h2>
        <p>Seguimiento de pagos, deudas e ingresos al complejo</p>
      </div>

      <div className="tabs">
        <button className={`tab${tab === 'resumen' ? ' active' : ''}`} onClick={() => setTab('resumen')}>Resumen</button>
        <button className={`tab${tab === 'cobranza' ? ' active' : ''}`} onClick={() => setTab('cobranza')}>Cobranza</button>
        <button className={`tab${tab === 'ingresos' ? ' active' : ''}`} onClick={() => setTab('ingresos')}>Ingresos diarios</button>
        <button className={`tab${tab === 'servicios' ? ' active' : ''}`} onClick={() => setTab('servicios')}>Servicios más vendidos</button>
      </div>

      {tab !== 'ingresos' && (
        <div className="toolbar">
          <select className="form-control" style={{ width: 150 }} value={mes} onChange={e => setMes(Number(e.target.value))}>
            {MESES.map((m, i) => <option key={i} value={i + 1}>{m}</option>)}
          </select>
          <select className="form-control" style={{ width: 100 }} value={anio} onChange={e => setAnio(Number(e.target.value))}>
            {[anio - 1, anio, anio + 1].map(a => <option key={a} value={a}>{a}</option>)}
          </select>
        </div>
      )}

      {tab === 'resumen' && resumen && (
        <>
          <div className="card-grid">
            <div className="stat-card success">
              <div className="label">Total cobrado</div>
              <div className="value">{formatUYU(resumen.totalCobrado)}</div>
            </div>
            <div className="stat-card danger">
              <div className="label">Total pendiente</div>
              <div className="value">{formatUYU(resumen.totalPendiente)}</div>
            </div>
            <div className="stat-card warning">
              <div className="label">Cuotas pendientes</div>
              <div className="value">{resumen.cuotasPendientes}</div>
            </div>
            <div className="stat-card">
              <div className="label">Cargos clientes pendientes</div>
              <div className="value">{resumen.cargosPendientes}</div>
            </div>
            <div className="stat-card">
              <div className="label">Socios activos</div>
              <div className="value">{resumen.sociosActivos}</div>
            </div>
            <div className="stat-card success">
              <div className="label">Ingresos al spa hoy</div>
              <div className="value">{resumen.ingresosHoy}</div>
            </div>
          </div>
          <div className="card">
            <h3 style={{ marginBottom: '1rem' }}>Estado de cobranza — {MESES[mes - 1]} {anio}</h3>
            <p style={{ color: 'var(--color-text-muted)' }}>
              {resumen.totalPendiente > 0
                ? `Hay ${formatUYU(resumen.totalPendiente)} pendientes de cobro. Revisá la pestaña Cobranza para el detalle por socio.`
                : '¡Excelente! No hay deudas pendientes este mes.'}
            </p>
          </div>
        </>
      )}

      {tab === 'cobranza' && (
        <div className="card table-container">
          <table className="data-table">
            <thead>
              <tr>
                <th>Nº Socio</th><th>Nombre</th><th>Estado cuota</th>
                <th>Pagado</th><th>Pendiente</th><th>Cargos extra</th>
              </tr>
            </thead>
            <tbody>
              {cobranza.map(c => (
                <tr key={c.socioId}>
                  <td><strong>{c.numeroSocio}</strong></td>
                  <td>{c.nombreCompleto}</td>
                  <td>{pagoBadge(c.estadoCuotaMes)}</td>
                  <td>{formatUYU(c.totalPagado)}</td>
                  <td style={{ color: c.totalPendiente > 0 ? 'var(--color-danger)' : 'inherit', fontWeight: 600 }}>
                    {formatUYU(c.totalPendiente)}
                  </td>
                  <td>
                    {c.cargosPendientes.length > 0
                      ? c.cargosPendientes.map(cp => (
                          <div key={cp.id} style={{ fontSize: '0.8rem' }}>{cp.servicio}: {formatUYU(cp.monto)}</div>
                        ))
                      : '—'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {cobranza.length === 0 && <div className="empty-state">No hay deudas pendientes 🎉</div>}
        </div>
      )}

      {tab === 'ingresos' && (
        <>
          <div className="toolbar">
            <input type="date" className="form-control" style={{ width: 200 }} value={fechaIngresos} onChange={e => setFechaIngresos(e.target.value)} />
          </div>
          {ingresosLoading && <div className="empty-state">Cargando ingresos...</div>}
          {!ingresosLoading && ingresos && (
            <>
              <div className="card-grid" style={{ gridTemplateColumns: 'repeat(3, 1fr)' }}>
                <div className="stat-card"><div className="label">Total registros</div><div className="value">{ingresos.totalEntradas}</div></div>
                <div className="stat-card success"><div className="label">Accesos permitidos</div><div className="value">{ingresos.accesosPermitidos}</div></div>
                <div className="stat-card danger"><div className="label">Accesos rechazados</div><div className="value">{ingresos.accesosRechazados}</div></div>
              </div>
              <div className="card table-container">
                <table className="data-table">
                  <thead>
                    <tr><th>Hora</th><th>Nº Socio</th><th>Nombre</th><th>Tipo</th><th>Resultado</th><th>Motivo</th></tr>
                  </thead>
                  <tbody>
                    {ingresos.detalle.map(i => (
                      <tr key={i.id}>
                        <td>{formatHora(i.fechaHora)}</td>
                        <td>{i.numeroSocio}</td>
                        <td>{i.socioNombre}</td>
                        <td>{i.tipo}</td>
                        <td>
                          <span className={`badge ${i.accesoPermitido ? 'badge-success' : 'badge-danger'}`}>
                            {i.accesoPermitido ? 'Permitido' : 'Rechazado'}
                          </span>
                        </td>
                        <td>{i.motivoRechazo || '—'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
                {ingresos.detalle.length === 0 && <div className="empty-state">Sin registros para esta fecha</div>}
              </div>
            </>
          )}
        </>
      )}

      {tab === 'servicios' && (
        <div className="card table-container">
          <table className="data-table">
            <thead>
              <tr><th>#</th><th>Servicio</th><th>Cantidad</th><th>Total facturado</th></tr>
            </thead>
            <tbody>
              {serviciosTop.map((s, i) => (
                <tr key={i}>
                  <td>{i + 1}</td>
                  <td><strong>{s.nombre}</strong></td>
                  <td>{s.cantidad}</td>
                  <td>{formatUYU(s.total)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {serviciosTop.length === 0 && <div className="empty-state">Sin datos para este período</div>}
        </div>
      )}
    </div>
  );
}
