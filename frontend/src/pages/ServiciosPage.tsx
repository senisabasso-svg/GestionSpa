import { useEffect, useState } from 'react';
import { api } from '../api/client';
import type { Servicio, CategoriaServicio } from '../types';
import { formatUYU } from '../types';
import { validateServicio, LIMITS } from '../utils/validation';
import { Plus } from 'lucide-react';

const categorias: CategoriaServicio[] = ['Masajes', 'Termal', 'Facial', 'Corporal', 'Paquetes', 'Otros'];
const emptyForm = { nombre: '', descripcion: '', categoria: 'Masajes' as CategoriaServicio, precio: 0, duracionMinutos: 30, soloSocios: false };

export default function ServiciosPage() {
  const [servicios, setServicios] = useState<Servicio[]>([]);
  const [modal, setModal] = useState(false);
  const [editId, setEditId] = useState<number | null>(null);
  const [form, setForm] = useState(emptyForm);
  const [errors, setErrors] = useState<string[]>([]);

  const load = () => api.servicios.list().then(setServicios).catch(console.error);
  useEffect(() => { load(); }, []);

  const openNew = () => { setEditId(null); setForm(emptyForm); setErrors([]); setModal(true); };
  const openEdit = (s: Servicio) => {
    setEditId(s.id);
    setForm({ nombre: s.nombre, descripcion: s.descripcion || '', categoria: s.categoria, precio: s.precio, duracionMinutos: s.duracionMinutos, soloSocios: s.soloSocios });
    setErrors([]); setModal(true);
  };

  const save = async () => {
    setErrors([]);
    const validationErrors = validateServicio(form);
    if (validationErrors.length > 0) { setErrors(validationErrors); return; }
    try {
      const payload = {
        nombre: form.nombre.trim(),
        descripcion: form.descripcion.trim() || null,
        categoria: form.categoria,
        precio: form.precio,
        duracionMinutos: form.duracionMinutos,
        soloSocios: Boolean(form.soloSocios),
      };
      if (editId) await api.servicios.update(editId, payload);
      else await api.servicios.create(payload);
      setModal(false);
      load();
    } catch (e) {
      setErrors([e instanceof Error ? e.message : 'Error al guardar']);
    }
  };

  const toggle = async (id: number) => { await api.servicios.toggle(id); load(); };

  return (
    <div>
      <div className="page-header">
        <h2>Servicios</h2>
        <p>Catálogo de servicios o productos que ofrecés a socios y clientes</p>
      </div>

      <div className="toolbar">
        <button className="btn btn-primary" onClick={openNew}><Plus size={16} /> Nuevo Servicio</button>
      </div>

      <div className="card table-container">
        <table>
          <thead>
            <tr><th>Servicio</th><th>Categoría</th><th>Precio</th><th>Duración</th><th>Exclusivo socios</th><th>Estado</th><th>Acciones</th></tr>
          </thead>
          <tbody>
            {servicios.map(s => (
              <tr key={s.id}>
                <td>
                  <strong>{s.nombre}</strong>
                  {s.descripcion && <div style={{ fontSize: '0.8rem', color: 'var(--color-text-muted)' }}>{s.descripcion}</div>}
                </td>
                <td><span className="badge badge-info">{s.categoria}</span></td>
                <td>{formatUYU(s.precio)}</td>
                <td>{s.duracionMinutos > 0 ? `${s.duracionMinutos} min` : '—'}</td>
                <td>{s.soloSocios ? 'Sí' : 'No'}</td>
                <td><span className={`badge ${s.activo ? 'badge-success' : 'badge-neutral'}`}>{s.activo ? 'Activo' : 'Inactivo'}</span></td>
                <td>
                  <button className="btn btn-sm btn-secondary" onClick={() => openEdit(s)}>Editar</button>
                  <button className="btn btn-sm btn-secondary" style={{ marginLeft: 4 }} onClick={() => toggle(s.id)}>{s.activo ? 'Desactivar' : 'Activar'}</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {modal && (
        <div className="modal-overlay" onClick={() => setModal(false)}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <h3>{editId ? 'Editar Servicio' : 'Nuevo Servicio'}</h3>
            {errors.length > 0 && (
              <div className="alert alert-error">
                <ul style={{ margin: 0, paddingLeft: '1.2rem' }}>{errors.map((err, i) => <li key={i}>{err}</li>)}</ul>
              </div>
            )}
            <div className="form-group">
              <label>Nombre *</label>
              <input className="form-control" maxLength={LIMITS.nombre} value={form.nombre} onChange={e => setForm({ ...form, nombre: e.target.value })} />
            </div>
            <div className="form-group">
              <label>Descripción</label>
              <textarea className="form-control" rows={2} value={form.descripcion} onChange={e => setForm({ ...form, descripcion: e.target.value })} />
            </div>
            <div className="form-row">
              <div className="form-group">
                <label>Categoría</label>
                <select className="form-control" value={form.categoria} onChange={e => setForm({ ...form, categoria: e.target.value as CategoriaServicio })}>
                  {categorias.map(c => <option key={c} value={c}>{c}</option>)}
                </select>
              </div>
              <div className="form-group">
                <label>Precio (UYU)</label>
                <input className="form-control" type="number" min={1} value={form.precio} onChange={e => setForm({ ...form, precio: Number(e.target.value) })} />
              </div>
            </div>
            <div className="form-row">
              <div className="form-group">
                <label>Duración (minutos)</label>
                <input className="form-control" type="number" min={0} value={form.duracionMinutos} onChange={e => setForm({ ...form, duracionMinutos: Number(e.target.value) })} />
              </div>
              <div className="form-group" style={{ display: 'flex', alignItems: 'flex-end' }}>
                <label className="form-check">
                  <input type="checkbox" checked={form.soloSocios} onChange={e => setForm({ ...form, soloSocios: e.target.checked })} />
                  Solo para socios
                </label>
              </div>
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
