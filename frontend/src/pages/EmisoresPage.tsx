import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import type { Emisor } from '../types';
import { useAuth } from '../context/AuthContext';
import { Plus, Edit2 } from 'lucide-react';

const emptyForm = { nombre: '', slug: '', ciudad: '', departamento: '' };

const slugify = (s: string) => s.toLowerCase()
  .normalize('NFD').replace(/[\u0300-\u036f]/g, '')
  .replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');

export default function EmisoresPage() {
  const { selectEmisor } = useAuth();
  const navigate = useNavigate();
  const [emisores, setEmisores] = useState<Emisor[]>([]);
  const [modal, setModal] = useState(false);
  const [editId, setEditId] = useState<number | null>(null);
  const [form, setForm] = useState(emptyForm);
  const [errors, setErrors] = useState<string[]>([]);

  const load = () => api.emisores.list().then(setEmisores).catch(console.error);
  useEffect(() => { load(); }, []);

  const openNew = () => { setEditId(null); setForm(emptyForm); setErrors([]); setModal(true); };

  const openEdit = (e: Emisor) => {
    setEditId(e.id);
    setForm({ nombre: e.nombre, slug: e.slug, ciudad: e.ciudad || '', departamento: e.departamento || '' });
    setErrors([]);
    setModal(true);
  };

  const save = async () => {
    setErrors([]);
    if (!form.nombre.trim()) { setErrors(['El nombre es obligatorio']); return; }
    if (!form.slug.trim()) { setErrors(['El slug es obligatorio']); return; }
    try {
      const payload = {
        nombre: form.nombre.trim(),
        slug: form.slug.trim().toLowerCase(),
        ciudad: form.ciudad.trim() || null,
        departamento: form.departamento.trim() || null,
      };
      if (editId) await api.emisores.update(editId, payload);
      else await api.emisores.create(payload);
      setModal(false);
      load();
    } catch (e) {
      setErrors([e instanceof Error ? e.message : 'Error al guardar']);
    }
  };

  const toggleActivo = async (e: Emisor) => {
    await api.emisores.toggleActivo(e.id, !e.activo);
    load();
  };

  return (
    <div>
      <div className="page-header">
        <h2>Emisores</h2>
        <p>Administrá los spas / gestiones independientes del sistema</p>
      </div>

      <div className="toolbar">
        <button className="btn btn-primary" onClick={openNew}><Plus size={16} /> Nuevo Emisor</button>
      </div>

      <div className="card table-container">
        <table className="data-table">
          <thead>
            <tr><th>Nombre</th><th>Slug</th><th>Ciudad</th><th>Estado</th><th>Acciones</th></tr>
          </thead>
          <tbody>
            {emisores.map(e => (
              <tr key={e.id}>
                <td><strong>{e.nombre}</strong></td>
                <td><code>{e.slug}</code></td>
                <td>{e.ciudad || '—'}</td>
                <td><span className={`badge ${e.activo ? 'badge-success' : 'badge-neutral'}`}>{e.activo ? 'Activo' : 'Inactivo'}</span></td>
                <td>
                  <button className="btn btn-sm btn-primary" onClick={() => { selectEmisor(e.id, e.nombre, e.slug); navigate('/'); }}>Gestionar</button>
                  <button className="btn btn-sm btn-secondary" style={{ marginLeft: 4 }} onClick={() => openEdit(e)}><Edit2 size={14} /></button>
                  <button className="btn btn-sm btn-secondary" style={{ marginLeft: 4 }} onClick={() => toggleActivo(e)}>
                    {e.activo ? 'Desactivar' : 'Activar'}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {emisores.length === 0 && <div className="empty-state">No hay emisores registrados</div>}
      </div>

      {modal && (
        <div className="modal-overlay" onClick={() => setModal(false)}>
          <div className="modal" onClick={ev => ev.stopPropagation()}>
            <h3>{editId ? 'Editar Emisor' : 'Nuevo Emisor'}</h3>
            {errors.length > 0 && <div className="alert alert-error"><ul style={{ margin: 0, paddingLeft: '1.2rem' }}>{errors.map((err, i) => <li key={i}>{err}</li>)}</ul></div>}
            <div className="form-group">
              <label>Nombre *</label>
              <input className="form-control" value={form.nombre} onChange={e => setForm({ ...form, nombre: e.target.value, slug: editId ? form.slug : slugify(e.target.value) })} />
            </div>
            <div className="form-group">
              <label>Slug (URL kiosk) *</label>
              <input className="form-control" value={form.slug} onChange={e => setForm({ ...form, slug: e.target.value.toLowerCase() })} placeholder="spa-dayman" />
            </div>
            <div className="form-row">
              <div className="form-group">
                <label>Ciudad</label>
                <input className="form-control" value={form.ciudad} onChange={e => setForm({ ...form, ciudad: e.target.value })} />
              </div>
              <div className="form-group">
                <label>Departamento</label>
                <input className="form-control" value={form.departamento} onChange={e => setForm({ ...form, departamento: e.target.value })} />
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
