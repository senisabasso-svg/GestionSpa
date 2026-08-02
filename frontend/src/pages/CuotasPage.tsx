import { useEffect, useState } from 'react';
import { api } from '../api/client';
import type { CuotaMensual, EstadoPago } from '../types';
import { MESES, formatUYU } from '../types';

const pagoBadge = (estado: EstadoPago) => {
  const map: Record<EstadoPago, string> = {
    Pagado: 'badge-success', Pendiente: 'badge-warning', Parcial: 'badge-info', Anulado: 'badge-neutral',
  };
  return <span className={`badge ${map[estado]}`}>{estado}</span>;
};

export default function CuotasPage() {
  const [cuotas, setCuotas] = useState<CuotaMensual[]>([]);
  const [mes, setMes] = useState(new Date().getMonth() + 1);
  const [anio, setAnio] = useState(new Date().getFullYear());
  const [buscar, setBuscar] = useState('');
  const [buscarDebounced, setBuscarDebounced] = useState('');
  const [pagoModal, setPagoModal] = useState<CuotaMensual | null>(null);
  const [pagoForm, setPagoForm] = useState({ monto: 0, metodoPago: 'Efectivo', referencia: '', registradoPor: '' });
  const [pagoError, setPagoError] = useState('');
  const [infoMsg, setInfoMsg] = useState('');

  useEffect(() => {
    const t = setTimeout(() => setBuscarDebounced(buscar), 300);
    return () => clearTimeout(t);
  }, [buscar]);

  const load = () => api.cuotas.list(mes, anio, undefined, buscarDebounced || undefined).then(setCuotas).catch(console.error);
  useEffect(() => { load(); }, [mes, anio, buscarDebounced]);

  const generar = async () => {
    setInfoMsg('');
    const ahora = new Date();
    const esPasado = anio < ahora.getFullYear() || (anio === ahora.getFullYear() && mes < ahora.getMonth() + 1);
    if (esPasado && !confirm(`¿Generar cuotas para ${MESES[mes - 1]} ${anio}? Es un período pasado.`)) return;
    try {
      const res = await api.cuotas.generar(mes, anio);
      setInfoMsg(res.mensaje);
      load();
    } catch (e) {
      setInfoMsg(e instanceof Error ? e.message : 'Error al generar cuotas');
    }
  };

  const registrarPago = async () => {
    if (!pagoModal) return;
    setPagoError('');
    if (pagoForm.monto <= 0) { setPagoError('El monto debe ser mayor a 0'); return; }
    try {
      await api.cuotas.pagar(pagoModal.id, pagoForm);
      setPagoModal(null);
      load();
    } catch (e) {
      setPagoError(e instanceof Error ? e.message : 'Error al registrar pago');
    }
  };

  return (
    <div>
      <div className="page-header">
        <h2>Cuotas Mensuales</h2>
        <p>Gestión de cuotas de socios — vencimiento día 10 de cada mes</p>
      </div>

      <div className="toolbar">
        <div className="search">
          <input
            className="form-control"
            placeholder="Buscar por nombre y apellido o nº socio..."
            value={buscar}
            onChange={e => setBuscar(e.target.value)}
            maxLength={100}
          />
        </div>
        <select className="form-control" style={{ width: 150 }} value={mes} onChange={e => setMes(Number(e.target.value))}>
          {MESES.map((m, i) => <option key={i} value={i + 1}>{m}</option>)}
        </select>
        <select className="form-control" style={{ width: 100 }} value={anio} onChange={e => setAnio(Number(e.target.value))}>
          {[anio - 1, anio, anio + 1].map(a => <option key={a} value={a}>{a}</option>)}
        </select>
        <button className="btn btn-secondary" onClick={generar}>Generar cuotas del mes</button>
      </div>

      {infoMsg && <div className="alert alert-success">{infoMsg}</div>}

      <div className="card table-container">
        <table className="data-table">
          <thead>
            <tr>
              <th>Nº Socio</th>
              <th className="col-socio">Socio</th>
              <th>Cuota base</th>
              <th>Servicios</th>
              <th>Total</th>
              <th>Pagado</th>
              <th>Saldo</th>
              <th>Estado</th>
              <th>Acciones</th>
            </tr>
          </thead>
          <tbody>
            {cuotas.map(c => (
              <tr key={c.id}>
                <td><strong>{c.numeroSocio}</strong></td>
                <td className="cell-ellipsis col-socio" title={c.socioNombre}>{c.socioNombre}</td>
                <td>{formatUYU(c.montoCuota)}</td>
                <td>{formatUYU(c.montoServicios)}</td>
                <td><strong>{formatUYU(c.total)}</strong></td>
                <td>{formatUYU(c.montoPagado)}</td>
                <td style={{ color: c.saldoPendiente > 0 ? 'var(--color-danger)' : 'inherit' }}>{formatUYU(c.saldoPendiente)}</td>
                <td>{pagoBadge(c.estadoPago)}</td>
                <td>
                  {c.estadoPago !== 'Pagado' && (
                    <button className="btn btn-sm btn-success" onClick={() => { setPagoModal(c); setPagoForm({ monto: c.saldoPendiente, metodoPago: 'Efectivo', referencia: '', registradoPor: '' }); }}>
                      Cobrar
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {cuotas.length === 0 && (
          <div className="empty-state">
            {buscarDebounced
              ? 'No hay cuotas que coincidan con la búsqueda.'
              : 'No hay cuotas para este período. Generá las cuotas del mes.'}
          </div>
        )}
      </div>

      {pagoModal && (
        <div className="modal-overlay" onClick={() => setPagoModal(null)}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <h3>Registrar Pago de Cuota</h3>
            <p style={{ marginBottom: '1rem' }}>
              <strong>{pagoModal.socioNombre}</strong> — {MESES[pagoModal.mes - 1]} {pagoModal.anio}
              <br />Saldo pendiente: <strong>{formatUYU(pagoModal.saldoPendiente)}</strong>
            </p>
            {pagoError && <div className="alert alert-error">{pagoError}</div>}
            <div className="form-group">
              <label>Monto (UYU)</label>
              <input className="form-control" type="number" min={1} value={pagoForm.monto} onChange={e => setPagoForm({ ...pagoForm, monto: Number(e.target.value) })} />
            </div>
            <div className="form-group">
              <label>Método de pago</label>
              <select className="form-control" value={pagoForm.metodoPago} onChange={e => setPagoForm({ ...pagoForm, metodoPago: e.target.value })}>
                <option value="Efectivo">Efectivo</option>
                <option value="TarjetaDebito">Tarjeta de débito</option>
                <option value="TarjetaCredito">Tarjeta de crédito</option>
                <option value="Transferencia">Transferencia bancaria</option>
                <option value="MercadoPago">Mercado Pago</option>
              </select>
            </div>
            <div className="modal-actions">
              <button className="btn btn-secondary" onClick={() => setPagoModal(null)}>Cancelar</button>
              <button className="btn btn-success" onClick={registrarPago}>Confirmar Pago</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
