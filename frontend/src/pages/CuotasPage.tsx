import { useEffect, useState } from 'react';
import { api } from '../api/client';
import type { CuotaMensual, EstadoPago } from '../types';
import { MESES, formatUYU } from '../types';

const pagoBadge = (estado: EstadoPago) => {
  const map: Record<EstadoPago, string> = { Pagado: 'badge-success', Pendiente: 'badge-warning', Parcial: 'badge-info' };
  return <span className={`badge ${map[estado]}`}>{estado}</span>;
};

export default function CuotasPage() {
  const [cuotas, setCuotas] = useState<CuotaMensual[]>([]);
  const [mes, setMes] = useState(new Date().getMonth() + 1);
  const [anio, setAnio] = useState(new Date().getFullYear());
  const [pagoModal, setPagoModal] = useState<CuotaMensual | null>(null);
  const [pagoForm, setPagoForm] = useState({ monto: 0, metodoPago: 'Efectivo', referencia: '', registradoPor: '' });
  const [pagoError, setPagoError] = useState('');

  const load = () => api.cuotas.list(mes, anio).then(setCuotas).catch(console.error);
  useEffect(() => { load(); }, [mes, anio]);

  const generar = async () => {
    const ahora = new Date();
    const esPasado = anio < ahora.getFullYear() || (anio === ahora.getFullYear() && mes < ahora.getMonth() + 1);
    if (esPasado && !confirm(`¿Generar cuotas para ${MESES[mes - 1]} ${anio}? Es un período pasado.`)) return;
    await api.cuotas.generar(mes, anio);
    load();
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
        <select className="form-control" style={{ width: 150 }} value={mes} onChange={e => setMes(Number(e.target.value))}>
          {MESES.map((m, i) => <option key={i} value={i + 1}>{m}</option>)}
        </select>
        <select className="form-control" style={{ width: 100 }} value={anio} onChange={e => setAnio(Number(e.target.value))}>
          {[anio - 1, anio, anio + 1].map(a => <option key={a} value={a}>{a}</option>)}
        </select>
        <button className="btn btn-secondary" onClick={generar}>Generar cuotas del mes</button>
      </div>

      <div className="card table-container">
        <table className="data-table">
          <thead>
            <tr>
              <th>Nº Socio</th><th>Socio</th><th>Cuota base</th><th>Servicios</th>
              <th>Total</th><th>Pagado</th><th>Saldo</th><th>Estado</th><th>Acciones</th>
            </tr>
          </thead>
          <tbody>
            {cuotas.map(c => (
              <tr key={c.id}>
                <td><strong>{c.numeroSocio}</strong></td>
                <td>{c.socioNombre}</td>
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
        {cuotas.length === 0 && <div className="empty-state">No hay cuotas para este período. Generá las cuotas del mes.</div>}
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
