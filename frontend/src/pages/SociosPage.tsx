import { useEffect, useState } from 'react';
import { api } from '../api/client';
import type { Socio, EstadoSocio, MetodoPago } from '../types';
import { formatUYU, formatFecha, METODOS_PAGO, labelMetodoPago } from '../types';
import { validateSocio, LIMITS } from '../utils/validation';
import { Plus, Edit2 } from 'lucide-react';

const hoy = () => new Date().toISOString().split('T')[0];

const estadoBadge = (estado: EstadoSocio) => {
  const map: Record<EstadoSocio, string> = { Activo: 'badge-success', Suspendido: 'badge-warning', Inactivo: 'badge-neutral' };
  return <span className={`badge ${map[estado]}`}>{estado}</span>;
};

interface SocioForm {
  nombre: string;
  apellido: string;
  cedula: string;
  telefono: string;
  email: string;
  medioPago: MetodoPago;
  fechaAlta: string;
  fechaVencimiento: string;
  cuotaMensual: number;
}

const emptyForm = (): SocioForm => ({
  nombre: '', apellido: '', cedula: '', telefono: '', email: '',
  medioPago: 'Efectivo', fechaAlta: hoy(), fechaVencimiento: '', cuotaMensual: 3500,
});

const toPayload = (form: SocioForm) => ({
  nombre: form.nombre.trim(), apellido: form.apellido.trim(), cedula: form.cedula.trim(),
  telefono: form.telefono.trim() || null, email: form.email.trim() || null,
  medioPago: form.medioPago, fechaAlta: form.fechaAlta,
  fechaVencimiento: form.fechaVencimiento || null, cuotaMensual: form.cuotaMensual,
});

export default function SociosPage() {
  const [socios, setSocios] = useState<Socio[]>([]);
  const [buscar, setBuscar] = useState('');
  const [modal, setModal] = useState(false);
  const [editId, setEditId] = useState<number | null>(null);
  const [editNumero, setEditNumero] = useState('');
  const [form, setForm] = useState<SocioForm>(emptyForm);
  const [errors, setErrors] = useState<string[]>([]);
  const [confirmSuspend, setConfirmSuspend] = useState<Socio | null>(null);

  const load = () => api.socios.list(buscar || undefined).then(setSocios).catch(console.error);
  useEffect(() => { load(); }, [buscar]);

  const openNew = () => {
    setEditId(null); setEditNumero(''); setForm(emptyForm()); setErrors([]); setModal(true);
  };

  const openEdit = (s: Socio) => {
    setEditId(s.id); setEditNumero(s.numeroSocio);
    setForm({
      nombre: s.nombre, apellido: s.apellido, cedula: s.cedula,
      telefono: s.telefono || '', email: s.email || '', medioPago: s.medioPago,
      fechaAlta: s.fechaAlta.split('T')[0],
      fechaVencimiento: s.fechaVencimiento?.split('T')[0] || '',
      cuotaMensual: s.cuotaMensual,
    });
    setErrors([]); setModal(true);
  };

  const save = async () => {
    setErrors([]);
    const validationErrors = validateSocio(form);
    if (validationErrors.length > 0) { setErrors(validationErrors); return; }
    try {
      const payload = toPayload(form);
      if (editId) await api.socios.update(editId, payload);
      else await api.socios.create(payload);
      setModal(false);
      load();
    } catch (e) {
      setErrors([e instanceof Error ? e.message : 'Error al guardar']);
    }
  };

  const cambiarEstado = async (id: number, estado: EstadoSocio) => {
    await api.socios.cambiarEstado(id, estado);
    setConfirmSuspend(null);
    load();
  };

  return (
    <div>
      <div className="page-header">
        <h2>Gestión de Socios</h2>
        <p>Administrá los socios del SPA Thermal Daymán</p>
      </div>

      <div className="toolbar">
        <div className="search">
          <input className="form-control" placeholder="Buscar por nombre, cédula o número..." value={buscar} onChange={e => setBuscar(e.target.value)} maxLength={100} />
        </div>
        <button className="btn btn-primary" onClick={openNew}><Plus size={16} /> Nuevo Socio</button>
      </div>

      <div className="card table-container">
        <table className="data-table">
          <thead>
            <tr>
              <th>Nº Socio</th><th>Nombre</th><th>Cédula</th><th>Teléfono</th>
              <th>Medio de pago</th><th>Cuota</th><th>Alta</th><th>Vencimiento</th><th>Estado</th><th>Acciones</th>
            </tr>
          </thead>
          <tbody>
            {socios.map(s => (
              <tr key={s.id}>
                <td><strong>{s.numeroSocio}</strong></td>
                <td className="cell-ellipsis" title={`${s.nombre} ${s.apellido}`}>{s.nombre} {s.apellido}</td>
                <td>{s.cedula}</td>
                <td>{s.telefono || '—'}</td>
                <td>{labelMetodoPago(s.medioPago)}</td>
                <td>{formatUYU(s.cuotaMensual)}</td>
                <td>{formatFecha(s.fechaAlta)}</td>
                <td>{s.fechaVencimiento ? formatFecha(s.fechaVencimiento) : '—'}</td>
                <td>{estadoBadge(s.estado)}</td>
                <td>
                  <button className="btn btn-sm btn-secondary" onClick={() => openEdit(s)}><Edit2 size={14} /></button>
                  {s.estado === 'Activo' && (
                    <button className="btn btn-sm btn-secondary" style={{ marginLeft: 4 }} onClick={() => setConfirmSuspend(s)}>Suspender</button>
                  )}
                  {s.estado === 'Suspendido' && (
                    <button className="btn btn-sm btn-success" style={{ marginLeft: 4 }} onClick={() => cambiarEstado(s.id, 'Activo')}>Activar</button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {socios.length === 0 && <div className="empty-state">No hay socios registrados</div>}
      </div>

      {modal && (
        <div className="modal-overlay" onClick={() => setModal(false)}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <h3>{editId ? 'Editar Socio' : 'Nuevo Socio'}</h3>
            {editId && <p style={{ marginBottom: '1rem', color: 'var(--color-text-muted)', fontSize: '0.9rem' }}>Nº de socio: <strong>{editNumero}</strong></p>}
            {errors.length > 0 && (
              <div className="alert alert-error">
                <ul style={{ margin: 0, paddingLeft: '1.2rem' }}>{errors.map((err, i) => <li key={i}>{err}</li>)}</ul>
              </div>
            )}
            <div className="form-row">
              <div className="form-group">
                <label>Nombre *</label>
                <input className="form-control" maxLength={LIMITS.nombre} value={form.nombre} onChange={e => setForm({ ...form, nombre: e.target.value })} />
              </div>
              <div className="form-group">
                <label>Apellido *</label>
                <input className="form-control" maxLength={LIMITS.apellido} value={form.apellido} onChange={e => setForm({ ...form, apellido: e.target.value })} />
              </div>
            </div>
            <div className="form-row">
              <div className="form-group">
                <label>Cédula de Identidad *</label>
                <input className="form-control" maxLength={LIMITS.cedula} placeholder="1.234.567-8" value={form.cedula} onChange={e => setForm({ ...form, cedula: e.target.value })} />
              </div>
              <div className="form-group">
                <label>Teléfono</label>
                <input className="form-control" maxLength={LIMITS.telefono} placeholder="099 123 456" value={form.telefono} onChange={e => setForm({ ...form, telefono: e.target.value })} />
              </div>
            </div>
            <div className="form-group">
              <label>Email</label>
              <input className="form-control" type="email" maxLength={LIMITS.email} value={form.email} onChange={e => setForm({ ...form, email: e.target.value })} />
            </div>
            <div className="form-row">
              <div className="form-group">
                <label>Medio de pago</label>
                <select className="form-control" value={form.medioPago} onChange={e => setForm({ ...form, medioPago: e.target.value as MetodoPago })}>
                  {METODOS_PAGO.map(m => <option key={m.value} value={m.value}>{m.label}</option>)}
                </select>
              </div>
              <div className="form-group">
                <label>Monto de cuota mensual (UYU) *</label>
                <input className="form-control" type="number" min={1} value={form.cuotaMensual} onChange={e => setForm({ ...form, cuotaMensual: Number(e.target.value) })} />
              </div>
            </div>
            <div className="form-row">
              <div className="form-group">
                <label>Fecha de alta *</label>
                <input className="form-control" type="date" max={hoy()} value={form.fechaAlta} onChange={e => setForm({ ...form, fechaAlta: e.target.value })} />
              </div>
              <div className="form-group">
                <label>Fecha de vencimiento</label>
                <input className="form-control" type="date" min={form.fechaAlta || undefined} value={form.fechaVencimiento} onChange={e => setForm({ ...form, fechaVencimiento: e.target.value })} />
              </div>
            </div>
            <div className="modal-actions">
              <button className="btn btn-secondary" onClick={() => setModal(false)}>Cancelar</button>
              <button className="btn btn-primary" onClick={save}>Guardar</button>
            </div>
          </div>
        </div>
      )}

      {confirmSuspend && (
        <div className="modal-overlay" onClick={() => setConfirmSuspend(null)}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <h3>Confirmar suspensión</h3>
            <p>¿Estás seguro que deseas suspender a <strong>{confirmSuspend.nombre} {confirmSuspend.apellido}</strong>?</p>
            <div className="modal-actions">
              <button className="btn btn-secondary" onClick={() => setConfirmSuspend(null)}>Cancelar</button>
              <button className="btn btn-danger" onClick={() => cambiarEstado(confirmSuspend.id, 'Suspendido')}>Suspender</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
