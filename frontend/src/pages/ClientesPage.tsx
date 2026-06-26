import { useEffect, useState } from 'react';
import { api } from '../api/client';
import type { Cliente } from '../types';
import { formatFecha } from '../types';
import { Plus } from 'lucide-react';

const emptyForm = { nombre: '', apellido: '', cedula: '', telefono: '', email: '', observaciones: '' };

export default function ClientesPage() {
  const [clientes, setClientes] = useState<Cliente[]>([]);
  const [buscar, setBuscar] = useState('');
  const [modal, setModal] = useState(false);
  const [form, setForm] = useState(emptyForm);
  const [error, setError] = useState('');

  const load = () => api.clientes.list(buscar || undefined).then(setClientes).catch(console.error);
  useEffect(() => { load(); }, [buscar]);

  const save = async () => {
    try {
      await api.clientes.create(form);
      setModal(false);
      setForm(emptyForm);
      load();
    } catch (e) { setError(e instanceof Error ? e.message : 'Error'); }
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
        <button className="btn btn-primary" onClick={() => { setModal(true); setError(''); }}><Plus size={16} /> Nuevo Cliente</button>
      </div>

      <div className="card table-container">
        <table>
          <thead>
            <tr><th>Nombre</th><th>Cédula</th><th>Teléfono</th><th>Email</th><th>Registro</th></tr>
          </thead>
          <tbody>
            {clientes.map(c => (
              <tr key={c.id}>
                <td><strong>{c.nombre} {c.apellido}</strong></td>
                <td>{c.cedula || '—'}</td>
                <td>{c.telefono || '—'}</td>
                <td>{c.email || '—'}</td>
                <td>{formatFecha(c.fechaRegistro)}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {clientes.length === 0 && <div className="empty-state">No hay clientes registrados</div>}
      </div>

      {modal && (
        <div className="modal-overlay" onClick={() => setModal(false)}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <h3>Nuevo Cliente</h3>
            {error && <div className="alert alert-error">{error}</div>}
            <div className="form-row">
              <div className="form-group">
                <label>Nombre *</label>
                <input className="form-control" value={form.nombre} onChange={e => setForm({ ...form, nombre: e.target.value })} />
              </div>
              <div className="form-group">
                <label>Apellido *</label>
                <input className="form-control" value={form.apellido} onChange={e => setForm({ ...form, apellido: e.target.value })} />
              </div>
            </div>
            <div className="form-row">
              <div className="form-group">
                <label>Cédula</label>
                <input className="form-control" value={form.cedula} onChange={e => setForm({ ...form, cedula: e.target.value })} />
              </div>
              <div className="form-group">
                <label>Teléfono</label>
                <input className="form-control" value={form.telefono} onChange={e => setForm({ ...form, telefono: e.target.value })} />
              </div>
            </div>
            <div className="form-group">
              <label>Email</label>
              <input className="form-control" type="email" value={form.email} onChange={e => setForm({ ...form, email: e.target.value })} />
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
