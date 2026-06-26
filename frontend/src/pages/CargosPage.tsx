import { useEffect, useState } from 'react';
import { api } from '../api/client';
import type { Cargo, Socio, Cliente, Servicio, EstadoPago } from '../types';
import { formatUYU, formatFecha } from '../types';
import { Plus } from 'lucide-react';

const pagoBadge = (estado: EstadoPago) => {
  const map: Record<EstadoPago, string> = { Pagado: 'badge-success', Pendiente: 'badge-warning', Parcial: 'badge-info' };
  return <span className={`badge ${map[estado]}`}>{estado}</span>;
};

export default function CargosPage() {
  const [cargos, setCargos] = useState<Cargo[]>([]);
  const [socios, setSocios] = useState<Socio[]>([]);
  const [clientes, setClientes] = useState<Cliente[]>([]);
  const [servicios, setServicios] = useState<Servicio[]>([]);
  const [modal, setModal] = useState(false);
  const [pagoModal, setPagoModal] = useState<Cargo | null>(null);
  const [tipo, setTipo] = useState<'socio' | 'cliente'>('socio');
  const [form, setForm] = useState({ servicioId: 0, socioId: 0, clienteId: 0, cantidad: 1, sumarACuota: true, notas: '', atendidoPor: '' });
  const [pagoForm, setPagoForm] = useState({ monto: 0, metodoPago: 'Efectivo', referencia: '', registradoPor: '' });
  const [error, setError] = useState('');

  const load = () => api.cargos.list().then(setCargos).catch(console.error);
  useEffect(() => {
    load();
    api.socios.list(undefined, 'Activo').then(setSocios);
    api.clientes.list().then(setClientes);
    api.servicios.list(true).then(setServicios);
  }, []);

  const save = async () => {
    try {
      const data = {
        servicioId: form.servicioId,
        socioId: tipo === 'socio' ? form.socioId : null,
        clienteId: tipo === 'cliente' ? form.clienteId : null,
        cantidad: form.cantidad,
        sumarACuota: tipo === 'socio' && form.sumarACuota,
        notas: form.notas,
        atendidoPor: form.atendidoPor,
      };
      await api.cargos.create(data);
      setModal(false);
      load();
    } catch (e) { setError(e instanceof Error ? e.message : 'Error'); }
  };

  const registrarPago = async () => {
    if (!pagoModal) return;
    await api.cargos.pagar(pagoModal.id, pagoForm);
    setPagoModal(null);
    load();
  };

  const servicioSel = servicios.find(s => s.id === form.servicioId);

  return (
    <div>
      <div className="page-header">
        <h2>Cargos de Servicios</h2>
        <p>Registrá servicios a socios (suma a cuota) o clientes ocasionales</p>
      </div>

      <div className="toolbar">
        <button className="btn btn-primary" onClick={() => { setModal(true); setError(''); }}><Plus size={16} /> Nuevo Cargo</button>
      </div>

      <div className="card table-container">
        <table>
          <thead>
            <tr><th>Fecha</th><th>Servicio</th><th>Persona</th><th>Tipo</th><th>Monto</th><th>Cuota</th><th>Estado</th><th>Acciones</th></tr>
          </thead>
          <tbody>
            {cargos.map(c => (
              <tr key={c.id}>
                <td>{formatFecha(c.fecha)}</td>
                <td>{c.servicioNombre}</td>
                <td>{c.socioNombre || c.clienteNombre}</td>
                <td><span className="badge badge-info">{c.socioId ? 'Socio' : 'Cliente'}</span></td>
                <td>{formatUYU(c.monto * c.cantidad)}</td>
                <td>{c.sumarACuota ? 'Sí' : 'No'}</td>
                <td>{pagoBadge(c.estadoPago)}</td>
                <td>
                  {c.estadoPago !== 'Pagado' && !c.sumarACuota && (
                    <button className="btn btn-sm btn-success" onClick={() => { setPagoModal(c); setPagoForm({ monto: c.monto * c.cantidad, metodoPago: 'Efectivo', referencia: '', registradoPor: '' }); }}>
                      Cobrar
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {cargos.length === 0 && <div className="empty-state">No hay cargos registrados</div>}
      </div>

      {modal && (
        <div className="modal-overlay" onClick={() => setModal(false)}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <h3>Nuevo Cargo de Servicio</h3>
            {error && <div className="alert alert-error">{error}</div>}

            <div className="tabs">
              <button className={`tab${tipo === 'socio' ? ' active' : ''}`} onClick={() => setTipo('socio')}>Socio</button>
              <button className={`tab${tipo === 'cliente' ? ' active' : ''}`} onClick={() => setTipo('cliente')}>Cliente ocasional</button>
            </div>

            {tipo === 'socio' ? (
              <div className="form-group">
                <label>Socio *</label>
                <select className="form-control" value={form.socioId} onChange={e => setForm({ ...form, socioId: Number(e.target.value) })}>
                  <option value={0}>Seleccionar socio...</option>
                  {socios.map(s => <option key={s.id} value={s.id}>{s.numeroSocio} — {s.nombre} {s.apellido}</option>)}
                </select>
              </div>
            ) : (
              <div className="form-group">
                <label>Cliente *</label>
                <select className="form-control" value={form.clienteId} onChange={e => setForm({ ...form, clienteId: Number(e.target.value) })}>
                  <option value={0}>Seleccionar cliente...</option>
                  {clientes.map(c => <option key={c.id} value={c.id}>{c.nombre} {c.apellido}</option>)}
                </select>
              </div>
            )}

            <div className="form-group">
              <label>Servicio *</label>
              <select className="form-control" value={form.servicioId} onChange={e => setForm({ ...form, servicioId: Number(e.target.value) })}>
                <option value={0}>Seleccionar servicio...</option>
                {servicios.filter(s => tipo === 'socio' || !s.soloSocios).map(s => (
                  <option key={s.id} value={s.id}>{s.nombre} — {formatUYU(s.precio)}</option>
                ))}
              </select>
            </div>

            {servicioSel && <div className="alert alert-success">Precio: {formatUYU(servicioSel.precio)} · {servicioSel.duracionMinutos} min</div>}

            <div className="form-row">
              <div className="form-group">
                <label>Cantidad</label>
                <input className="form-control" type="number" min={1} value={form.cantidad} onChange={e => setForm({ ...form, cantidad: Number(e.target.value) })} />
              </div>
              <div className="form-group">
                <label>Atendido por</label>
                <input className="form-control" value={form.atendidoPor} onChange={e => setForm({ ...form, atendidoPor: e.target.value })} />
              </div>
            </div>

            {tipo === 'socio' && (
              <div className="form-group">
                <label className="form-check">
                  <input type="checkbox" checked={form.sumarACuota} onChange={e => setForm({ ...form, sumarACuota: e.target.checked })} />
                  Sumar a la cuota mensual del socio
                </label>
              </div>
            )}

            <div className="form-group">
              <label>Notas</label>
              <textarea className="form-control" rows={2} value={form.notas} onChange={e => setForm({ ...form, notas: e.target.value })} />
            </div>

            <div className="modal-actions">
              <button className="btn btn-secondary" onClick={() => setModal(false)}>Cancelar</button>
              <button className="btn btn-primary" onClick={save}>Registrar Cargo</button>
            </div>
          </div>
        </div>
      )}

      {pagoModal && (
        <div className="modal-overlay" onClick={() => setPagoModal(null)}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <h3>Registrar Pago</h3>
            <p style={{ marginBottom: '1rem', color: 'var(--color-text-muted)' }}>
              {pagoModal.servicioNombre} — {pagoModal.clienteNombre || pagoModal.socioNombre}
            </p>
            <div className="form-group">
              <label>Monto (UYU)</label>
              <input className="form-control" type="number" value={pagoForm.monto} onChange={e => setPagoForm({ ...pagoForm, monto: Number(e.target.value) })} />
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
