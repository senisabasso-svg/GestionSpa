import { useEffect, useState } from 'react';
import { api } from '../api/client';
import type { Cliente } from '../types';
import { formatFecha } from '../types';
import { validateCliente, LIMITS } from '../utils/validation';
import { Plus, Edit2, Trash2 } from 'lucide-react';

const emptyForm = { nombre: '', apellido: '', cedula: '', telefono: '', email: '', observaciones: '' };

export default function ClientesPage() {
  const [clientes, setClientes] = useState<Cliente[]>([]);
  const [buscar, setBuscar] = useState('');
  const [modal, setModal] = useState(false);
  const [editId, setEditId] = useState<number | null>(null);
  const [form, setForm] = useState(emptyForm);
  const [errors, setErrors] = useState<string[]>([]);

  const load = () => api.clientes.list(buscar || undefined).then(setClientes).catch(console.error);
  useEffect(() => { load(); }, [buscar]);

  const openNew = () => { setEditId(null); setForm(emptyForm); setErrors([]); setModal(true); };
  const openEdit = (c: Cliente) => {
    setEditId(c.id);
    setForm({ nombre: c.nombre, apellido: c.apellido, cedula: c.cedula || '', telefono: c.telefono || '', email: c.email || '', observaciones: '' });
    setErrors([]); setModal(true);
  };

  const save = async () => {
    setErrors([]);
    const validationErrors = validateCliente(form);
    if (validationErrors.length > 0) { setErrors(validationErrors); return; }
    try {
      if (editId) await api.clientes.update(editId, form);
      else await api.clientes.create(form);
      setModal(false);
      load();
    } catch (e) {
      setErrors([e instanceof Error ? e.message : 'Error al guardar']);
    }
  };

  const remove = async (c: Cliente) => {
    if (!confirm(`¿Eliminar a ${c.nombre} ${c.apellido}?`)) return;
    await api.clientes.delete(c.id);
    load();
  };

  return (
    <div>
      <div className="page-header">
        <h2>Clientes Ocasionales</h2>
        <p>Personas que utilizan servicios sin ser socios</p>
      </div>

      <div className="toolbar">
        <div className="search">
          <input className="form-control" placeholder="Buscar cliente..." value={buscar} onChange={e => setBuscar(e.target.value)} />
        </div>
        <button className="btn btn-primary" onClick={openNew}><Plus size={16} /> Nuevo Cliente</button>
      </div>

      <div className="card table-container">
        <table className="data-table">
          <thead>
            <tr><th>Nombre</th><th>Cédula</th><th>Teléfono</th><th>Email</th><th>Registro</th><th>Acciones</th></tr>
          </thead>
          <tbody>
            {clientes.map(c => (
              <tr key={c.id}>
                <td className="cell-ellipsis"><strong>{c.nombre} {c.apellido}</strong></td>
                <td>{c.cedula || '—'}</td>
                <td>{c.telefono || '—'}</td>
                <td>{c.email || '—'}</td>
                <td>{formatFecha(c.fechaRegistro)}</td>
                <td>
                  <button className="btn btn-sm btn-secondary" onClick={() => openEdit(c)}><Edit2 size={14} /></button>
                  <button className="btn btn-sm btn-danger" style={{ marginLeft: 4 }} onClick={() => remove(c)}><Trash2 size={14} /></button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {clientes.length === 0 && <div className="empty-state">No hay clientes registrados</div>}
      </div>

      {modal && (
        <div className="modal-overlay" onClick={() => setModal(false)}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <h3>{editId ? 'Editar Cliente' : 'Nuevo Cliente'}</h3>
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
                <label>Cédula</label>
                <input className="form-control" maxLength={LIMITS.cedula} value={form.cedula} onChange={e => setForm({ ...form, cedula: e.target.value })} />
              </div>
              <div className="form-group">
                <label>Teléfono</label>
                <input className="form-control" maxLength={LIMITS.telefono} value={form.telefono} onChange={e => setForm({ ...form, telefono: e.target.value })} />
              </div>
            </div>
            <div className="form-group">
              <label>Email</label>
              <input className="form-control" type="email" maxLength={LIMITS.email} value={form.email} onChange={e => setForm({ ...form, email: e.target.value })} />
            </div>
            <div className="modal-actions">
              <button className="btn btn-secondary" onClick={() => setModal(false)}>Cancelar</button>
              <button className="btn btn-primary" onClick={save}>Guardar</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
