import { useEffect, useState } from 'react';
import { api } from '../api/client';
import type { InformeResumen, InformeCobranza, InformeSociosActivos, Ingreso, EstadoPago, ResultadoSorteo } from '../types';
import { MESES, formatUYU, formatHora, formatFecha, labelMetodoPago, fechaHoyLocal, LOCALIDAD_PENDIENTE } from '../types';
import { Download, Gift } from 'lucide-react';
import { useAuth } from '../context/AuthContext';

const inicioMes = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-01`;
};

const pagoBadge = (estado: EstadoPago | null | undefined, sinCuota?: boolean) => {
  if (sinCuota) return <span className="badge badge-neutral">Sin cuota</span>;
  if (!estado) return <span className="badge badge-neutral">—</span>;
  const map: Record<EstadoPago, string> = {
    Pagado: 'badge-success', Pendiente: 'badge-warning', Parcial: 'badge-info', Anulado: 'badge-neutral',
  };
  return <span className={`badge ${map[estado]}`}>{estado}</span>;
};

export default function InformesPage() {
  const { isSuperAdmin } = useAuth();
  const [tab, setTab] = useState<'resumen' | 'cobranza' | 'ingresos' | 'servicios' | 'sociosActivos'>('resumen');
  const [mes, setMes] = useState(new Date().getMonth() + 1);
  const [anio, setAnio] = useState(new Date().getFullYear());
  const [resumen, setResumen] = useState<InformeResumen | null>(null);
  const [cobranza, setCobranza] = useState<InformeCobranza[]>([]);
  const [ingresos, setIngresos] = useState<{ fecha: string; totalEntradas: number; accesosPermitidos: number; accesosRechazados: number; detalle: Ingreso[] } | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [serviciosTop, setServiciosTop] = useState<{ nombre: string; cantidad: number; total: number }[]>([]);
  const [sociosActivos, setSociosActivos] = useState<InformeSociosActivos | null>(null);
  const [exporting, setExporting] = useState(false);
  const [exportDesde, setExportDesde] = useState(inicioMes);
  const [exportHasta, setExportHasta] = useState(fechaHoyLocal);
  const [ganador, setGanador] = useState<ResultadoSorteo | null>(null);
  const [sorteando, setSorteando] = useState(false);
  const [fechaIngresos, setFechaIngresos] = useState(new Date().toISOString().split('T')[0]);

  useEffect(() => {
    setError('');
    setLoading(true);

    const load = async () => {
      try {
        if (tab === 'resumen') setResumen(await api.informes.resumen(mes, anio));
        if (tab === 'cobranza') setCobranza(await api.informes.cobranza(mes, anio));
        if (tab === 'ingresos') setIngresos(await api.informes.ingresosDiarios(fechaIngresos));
        if (tab === 'servicios') setServiciosTop(await api.informes.serviciosMasVendidos(mes, anio));
        if (tab === 'sociosActivos') setSociosActivos(await api.informes.sociosActivos(mes, anio));
      } catch (e) {
        setError(e instanceof Error ? e.message : 'Error al cargar informes');
      } finally {
        setLoading(false);
      }
    };

    load();
  }, [tab, mes, anio, fechaIngresos]);

  const exportarSociosActivos = async () => {
    setExporting(true);
    try {
      await api.informes.exportSociosActivos(mes, anio);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Error al exportar');
    } finally {
      setExporting(false);
    }
  };

  const exportarPorRango = async (tipo: 'activos' | 'inactivos') => {
    if (!exportDesde || !exportHasta) { setError('Indicá fecha desde y hasta'); return; }
    if (exportDesde > exportHasta) { setError('La fecha desde no puede ser posterior a la hasta'); return; }
    setExporting(true);
    setError('');
    try {
      await api.informes.exportSociosRango(tipo, exportDesde, exportHasta, mes, anio);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Error al exportar');
    } finally {
      setExporting(false);
    }
  };

  const generarSorteo = async () => {
    setSorteando(true);
    setError('');
    try {
      setGanador(await api.informes.sorteo());
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Error al generar sorteo');
    } finally {
      setSorteando(false);
    }
  };

  return (
    <div>
      <div className="page-header">
        <h2>Informes y Control de Cobranza</h2>
        <p>Seguimiento de pagos, deudas, ingresos y socios activos</p>
      </div>

      <div className="tabs">
        <button className={`tab${tab === 'resumen' ? ' active' : ''}`} onClick={() => setTab('resumen')}>Resumen</button>
        <button className={`tab${tab === 'cobranza' ? ' active' : ''}`} onClick={() => setTab('cobranza')}>Cobranza</button>
        <button className={`tab${tab === 'ingresos' ? ' active' : ''}`} onClick={() => setTab('ingresos')}>Ingresos diarios</button>
        <button className={`tab${tab === 'servicios' ? ' active' : ''}`} onClick={() => setTab('servicios')}>Servicios más vendidos</button>
        <button className={`tab${tab === 'sociosActivos' ? ' active' : ''}`} onClick={() => setTab('sociosActivos')}>Socios activos</button>
      </div>

      {tab !== 'ingresos' && (
        <div className="toolbar">
          <select className="form-control" style={{ width: 150 }} value={mes} onChange={e => setMes(Number(e.target.value))}>
            {MESES.map((m, i) => <option key={i} value={i + 1}>{m}</option>)}
          </select>
          <select className="form-control" style={{ width: 100 }} value={anio} onChange={e => setAnio(Number(e.target.value))}>
            {[anio - 1, anio, anio + 1].map(a => <option key={a} value={a}>{a}</option>)}
          </select>
          {tab === 'sociosActivos' && (
            <button className="btn btn-secondary" onClick={exportarSociosActivos} disabled={exporting}>
              <Download size={16} /> {exporting ? 'Exportando...' : 'Exportar CSV'}
            </button>
          )}
        </div>
      )}

      {error && <div className="alert alert-error">{error}</div>}
      {loading && <div className="empty-state">Cargando...</div>}

      {!loading && !error && tab === 'resumen' && resumen && (
        <>
          <div className="card-grid">
            <div className="stat-card success">
              <div className="label">Pagos del mes</div>
              <div className="value">{formatUYU(resumen.totalIngresosMes)}</div>
            </div>
            <div className="stat-card success">
              <div className="label">Cobrado en cuotas</div>
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
              <div className="label">Ingresos hoy</div>
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

      {!loading && !error && tab === 'cobranza' && (
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
                  <td className="cell-ellipsis" title={c.nombreCompleto}>{c.nombreCompleto}</td>
                  <td>{pagoBadge(c.estadoCuotaMes, c.sinCuotaMes)}</td>
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

      {!loading && !error && tab === 'ingresos' && (
        <>
          <div className="toolbar">
            <input type="date" className="form-control" style={{ width: 200 }} value={fechaIngresos} onChange={e => setFechaIngresos(e.target.value)} />
          </div>
          {ingresos && (
            <>
              <div className="card-grid" style={{ gridTemplateColumns: 'repeat(3, 1fr)' }}>
                <div className="stat-card"><div className="label">Entradas registradas</div><div className="value">{ingresos.totalEntradas}</div></div>
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
                        <td className="cell-ellipsis" title={i.socioNombre}>{i.socioNombre}</td>
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

      {!loading && !error && tab === 'servicios' && (
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

      {!loading && !error && tab === 'sociosActivos' && sociosActivos && (
        <>
          <div className="card" style={{ marginBottom: '1.25rem' }}>
            <h3 style={{ marginBottom: '0.75rem', color: 'var(--color-primary-dark)' }}>Exportar por rango de fechas</h3>
            <p style={{ color: 'var(--color-text-muted)', fontSize: '0.9rem', marginBottom: '1rem' }}>
              Filtra socios por <strong>fecha de alta</strong> en el período indicado.
            </p>
            <div className="form-row" style={{ marginBottom: '1rem' }}>
              <div className="form-group">
                <label>Desde</label>
                <input type="date" className="form-control" value={exportDesde} onChange={e => setExportDesde(e.target.value)} />
              </div>
              <div className="form-group">
                <label>Hasta</label>
                <input type="date" className="form-control" value={exportHasta} min={exportDesde} onChange={e => setExportHasta(e.target.value)} />
              </div>
            </div>
            <div style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap' }}>
              <button className="btn btn-secondary" onClick={() => exportarPorRango('activos')} disabled={exporting}>
                <Download size={16} /> {exporting ? 'Exportando...' : 'Exportar activos'}
              </button>
              <button className="btn btn-secondary" onClick={() => exportarPorRango('inactivos')} disabled={exporting}>
                <Download size={16} /> {exporting ? 'Exportando...' : 'Exportar inactivos'}
              </button>
            </div>
          </div>

          {isSuperAdmin && (
            <div className="card" style={{ marginBottom: '1.25rem' }}>
              <h3 style={{ marginBottom: '0.75rem', color: 'var(--color-primary-dark)' }}>Sorteo entre socios activos</h3>
              <p style={{ color: 'var(--color-text-muted)', fontSize: '0.9rem', marginBottom: '1rem' }}>
                Se elige al azar un ganador entre los <strong>{sociosActivos.resumen.totalActivos}</strong> socios activos actuales.
              </p>
              <button className="btn btn-primary" onClick={generarSorteo} disabled={sorteando || sociosActivos.resumen.totalActivos === 0}>
                <Gift size={16} /> {sorteando ? 'Sorteando...' : 'Generar sorteo'}
              </button>
              {ganador && (
                <div className="alert alert-success" style={{ marginTop: '1rem' }}>
                  <strong>¡Ganador del sorteo!</strong>
                  <div style={{ marginTop: '0.5rem', fontSize: '1.1rem' }}>
                    {ganador.nombreCompleto} <span style={{ color: 'var(--color-text-muted)' }}>(Nº {ganador.numeroSocio})</span>
                  </div>
                  <div style={{ marginTop: '0.35rem', fontSize: '0.9rem', color: 'var(--color-text-muted)' }}>
                    Documento: {ganador.cedula} · Participantes: {ganador.totalParticipantes}
                  </div>
                </div>
              )}
            </div>
          )}

          <div className="card-grid">
            <div className="stat-card">
              <div className="label">Total activos</div>
              <div className="value">{sociosActivos.resumen.totalActivos}</div>
            </div>
            <div className="stat-card success">
              <div className="label">Cuota pagada ({MESES[mes - 1]})</div>
              <div className="value">{sociosActivos.resumen.conCuotaPagada}</div>
            </div>
            <div className="stat-card warning">
              <div className="label">Cuota pendiente</div>
              <div className="value">{sociosActivos.resumen.conCuotaPendiente}</div>
            </div>
            <div className="stat-card danger">
              <div className="label">Sin cuota del mes</div>
              <div className="value">{sociosActivos.resumen.sinCuotaMes}</div>
            </div>
            <div className="stat-card">
              <div className="label">Con familia</div>
              <div className="value">{sociosActivos.resumen.conFamilia}</div>
            </div>
            <div className="stat-card">
              <div className="label">Sin familia</div>
              <div className="value">{sociosActivos.resumen.sinFamilia}</div>
            </div>
            <div className="stat-card success">
              <div className="label">Suma cuotas mensuales</div>
              <div className="value" style={{ fontSize: '1.35rem' }}>{formatUYU(sociosActivos.resumen.totalCuotasMensuales)}</div>
            </div>
          </div>
          <div className="card table-container">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Nº</th><th>Nombre</th><th>Localidad</th><th>Documento</th><th>Familia</th><th>Cuota</th>
                  <th>Medio pago</th><th>Alta</th><th>Estado cuota</th><th>Saldo mes</th>
                </tr>
              </thead>
              <tbody>
                {sociosActivos.socios.map(s => (
                  <tr key={s.id}>
                    <td><strong>{s.numeroSocio}</strong></td>
                    <td className="cell-ellipsis" title={`${s.nombre} ${s.apellido}`}>{s.nombre} {s.apellido}</td>
                    <td>
                      {!s.localidad || s.localidad.toLowerCase() === LOCALIDAD_PENDIENTE.toLowerCase() ? (
                        <span className="badge badge-warning" style={{ fontSize: '0.75rem' }}>{LOCALIDAD_PENDIENTE}</span>
                      ) : s.localidad}
                    </td>
                    <td>
                      {s.cedula}
                      {s.tipoIdentificacion === 'Otro' && (
                        <span className="badge badge-neutral" style={{ marginLeft: 6, fontSize: '0.7rem' }}>Otro</span>
                      )}
                    </td>
                    <td>{s.familiaNombre || '—'}</td>
                    <td>{formatUYU(s.cuotaMensual)}</td>
                    <td>{labelMetodoPago(s.medioPago)}</td>
                    <td>{formatFecha(s.fechaAlta)}</td>
                    <td>{pagoBadge(s.estadoCuotaMes, s.sinCuotaMes)}</td>
                    <td style={{ color: s.saldoCuotaMes > 0 ? 'var(--color-danger)' : 'inherit', fontWeight: s.saldoCuotaMes > 0 ? 600 : 400 }}>
                      {formatUYU(s.saldoCuotaMes)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            {sociosActivos.socios.length === 0 && <div className="empty-state">No hay socios activos</div>}
          </div>
        </>
      )}
    </div>
  );
}
