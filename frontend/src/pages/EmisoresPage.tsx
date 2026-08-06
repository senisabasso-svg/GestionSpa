import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import type { Emisor } from '../types';
import { useAuth } from '../context/AuthContext';
import { Plus, Edit2, Download, Upload, AlertTriangle } from 'lucide-react';
import type { PlatformBackupResumen, PlatformImportResult } from '../types';

const emptyForm = {
  nombre: '',
  slug: '',
  ciudad: '',
  departamento: '',
  adminEmail: '',
  adminPassword: '',
  adminPasswordConfirm: '',
  adminNombre: '',
  mostrarConfigPortero: false,
  mostrarSorteo: false,
  mostrarControlIngreso: true,
  mostrarAvisoPagoPendiente: false,
};

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
  const [exportandoPlataforma, setExportandoPlataforma] = useState(false);
  const [importandoPlataforma, setImportandoPlataforma] = useState(false);
  const [resumenPlataforma, setResumenPlataforma] = useState<PlatformBackupResumen | null>(null);
  const [archivoPlataforma, setArchivoPlataforma] = useState<File | null>(null);
  const [confirmarPlataforma, setConfirmarPlataforma] = useState(false);
  const [backupMsg, setBackupMsg] = useState<string | null>(null);
  const [backupError, setBackupError] = useState<string | null>(null);
  const [resultadoPlataforma, setResultadoPlataforma] = useState<PlatformImportResult | null>(null);

  const load = () => api.emisores.list().then(setEmisores).catch(console.error);
  const cargarResumenPlataforma = () => api.adminBackup.resumenPlatform().then(setResumenPlataforma).catch(() => setResumenPlataforma(null));
  useEffect(() => { load(); cargarResumenPlataforma(); }, []);

  const openNew = () => { setEditId(null); setForm(emptyForm); setErrors([]); setModal(true); };

  const openEdit = (e: Emisor) => {
    setEditId(e.id);
    setForm({
      nombre: e.nombre,
      slug: e.slug,
      ciudad: e.ciudad || '',
      departamento: e.departamento || '',
      adminEmail: '',
      adminPassword: '',
      adminPasswordConfirm: '',
      adminNombre: '',
      mostrarConfigPortero: !!e.mostrarConfigPortero,
      mostrarSorteo: !!e.mostrarSorteo,
      mostrarControlIngreso: e.mostrarControlIngreso !== false,
      mostrarAvisoPagoPendiente: !!e.mostrarAvisoPagoPendiente,
    });
    setErrors([]);
    setModal(true);
  };

  const save = async () => {
    setErrors([]);
    const errs: string[] = [];
    if (!form.nombre.trim()) errs.push('El nombre es obligatorio');
    if (!form.slug.trim()) errs.push('El slug es obligatorio');
    if (!editId) {
      if (!form.adminEmail.trim()) errs.push('El email del administrador es obligatorio');
      if (!form.adminNombre.trim()) errs.push('El nombre del administrador es obligatorio');
      if (form.adminPassword.length < 6) errs.push('La contraseña debe tener al menos 6 caracteres');
      if (form.adminPassword !== form.adminPasswordConfirm) errs.push('Las contraseñas no coinciden');
    }
    if (errs.length > 0) { setErrors(errs); return; }

    try {
      if (editId) {
        await api.emisores.update(editId, {
          nombre: form.nombre.trim(),
          slug: form.slug.trim().toLowerCase(),
          ciudad: form.ciudad.trim() || null,
          departamento: form.departamento.trim() || null,
          mostrarConfigPortero: form.mostrarConfigPortero,
          mostrarSorteo: form.mostrarSorteo,
          mostrarControlIngreso: form.mostrarControlIngreso,
          mostrarAvisoPagoPendiente: form.mostrarAvisoPagoPendiente,
        });
      } else {
        await api.emisores.create({
          nombre: form.nombre.trim(),
          slug: form.slug.trim().toLowerCase(),
          ciudad: form.ciudad.trim() || null,
          departamento: form.departamento.trim() || null,
          adminEmail: form.adminEmail.trim().toLowerCase(),
          adminPassword: form.adminPassword,
          adminNombre: form.adminNombre.trim(),
          mostrarConfigPortero: form.mostrarConfigPortero,
          mostrarSorteo: form.mostrarSorteo,
          mostrarControlIngreso: form.mostrarControlIngreso,
          mostrarAvisoPagoPendiente: form.mostrarAvisoPagoPendiente,
        });
      }
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

  const exportarPlataforma = async () => {
    setExportandoPlataforma(true);
    setBackupError(null);
    try {
      await api.adminBackup.exportPlatform();
      setBackupMsg('Respaldo de plataforma exportado correctamente');
    } catch (e) {
      setBackupError(e instanceof Error ? e.message : 'Error al exportar');
    } finally {
      setExportandoPlataforma(false);
    }
  };

  const importarPlataforma = async () => {
    if (!archivoPlataforma) { setBackupError('Seleccioná un archivo JSON'); return; }
    if (!confirmarPlataforma) { setBackupError('Debés confirmar que querés reemplazar toda la plataforma'); return; }
    setImportandoPlataforma(true);
    setBackupError(null);
    setBackupMsg(null);
    setResultadoPlataforma(null);
    try {
      const result = await api.adminBackup.importPlatform(archivoPlataforma);
      setResultadoPlataforma(result);
      setBackupMsg(result.mensaje);
      setArchivoPlataforma(null);
      setConfirmarPlataforma(false);
      load();
      cargarResumenPlataforma();
    } catch (e) {
      setBackupError(e instanceof Error ? e.message : 'Error al importar');
    } finally {
      setImportandoPlataforma(false);
    }
  };

  return (
    <div>
      <div className="page-header">
        <h2>Emisores</h2>
        <p>Administrá las empresas que usan el sistema de forma independiente</p>
      </div>

      {backupMsg && <div className="alert alert-success">{backupMsg}</div>}
      {backupError && <div className="alert alert-error">{backupError}</div>}

      <div className="card" style={{ marginBottom: '1.5rem' }}>
        <h3>Respaldo de plataforma completa</h3>
        <p className="text-muted" style={{ marginBottom: '1rem' }}>
          Exportá o importá <strong>todas las empresas</strong>, usuarios (incluidos SuperAdmin), socios, cuotas, cargos, pagos e ingresos.
          Ideal para clonar el servidor en otro entorno.
        </p>

        {resumenPlataforma && (
          <div style={{ marginBottom: '1rem', fontSize: '0.9rem', lineHeight: 1.7 }}>
            <strong>{resumenPlataforma.emisores}</strong> emisor(es) · <strong>{resumenPlataforma.superAdmins}</strong> SuperAdmin(s) ·{' '}
            <strong>{resumenPlataforma.totalSocios}</strong> socios · <strong>{resumenPlataforma.totalCuotas}</strong> cuotas
            {resumenPlataforma.detalle.length > 0 && (
              <ul style={{ margin: '0.5rem 0 0', paddingLeft: '1.25rem' }}>
                {resumenPlataforma.detalle.map(d => (
                  <li key={d.slug}><code>{d.slug}</code> — {d.nombre}: {d.socios} socios, {d.usuarios} usuarios</li>
                ))}
              </ul>
            )}
          </div>
        )}

        <button type="button" className="btn btn-primary" onClick={exportarPlataforma} disabled={exportandoPlataforma} style={{ marginBottom: '1.5rem' }}>
          <Download size={16} /> {exportandoPlataforma ? 'Exportando...' : 'Exportar plataforma completa'}
        </button>

        <hr style={{ margin: '1rem 0', border: 'none', borderTop: '1px solid var(--border, #e0e0e0)' }} />

        <h4 style={{ marginTop: 0 }}>Importar plataforma completa</h4>
        <div className="alert alert-error" style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-start' }}>
          <AlertTriangle size={18} style={{ flexShrink: 0, marginTop: 2 }} />
          <span>Esto <strong>borra y reemplaza todo</strong> el servidor: todas las empresas, usuarios y datos. Las contraseñas se restauran desde el hash del respaldo.</span>
        </div>

        <div className="form-group" style={{ marginTop: '1rem' }}>
          <label>Archivo JSON de plataforma</label>
          <input type="file" accept=".json,application/json" onChange={e => { setArchivoPlataforma(e.target.files?.[0] ?? null); setResultadoPlataforma(null); }} />
        </div>

        <label className="checkbox-label" style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: '0.75rem' }}>
          <input type="checkbox" checked={confirmarPlataforma} onChange={e => setConfirmarPlataforma(e.target.checked)} />
          Confirmo que quiero reemplazar toda la plataforma con este respaldo
        </label>

        <div style={{ marginTop: '1rem' }}>
          <button type="button" className="btn btn-secondary" onClick={importarPlataforma} disabled={importandoPlataforma || !archivoPlataforma}>
            <Upload size={16} /> {importandoPlataforma ? 'Importando...' : 'Importar plataforma completa'}
          </button>
        </div>

        {resultadoPlataforma && (
          <div className="alert alert-success" style={{ marginTop: '1rem' }}>
            {resultadoPlataforma.emisores} emisor(es), {resultadoPlataforma.superAdmins} SuperAdmin(s), {resultadoPlataforma.socios} socios, {resultadoPlataforma.cuotas} cuotas importados.
          </div>
        )}
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
                  <button className="btn btn-sm btn-primary" onClick={() => { selectEmisor(e.id, e.nombre, e.slug, e.mostrarConfigPortero, e.mostrarSorteo, e.mostrarControlIngreso !== false, !!e.mostrarAvisoPagoPendiente); navigate('/'); }}>Gestionar</button>
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
              <label>Nombre de la empresa *</label>
              <input
                className="form-control"
                value={form.nombre}
                onChange={e => setForm({
                  ...form,
                  nombre: e.target.value,
                  slug: editId ? form.slug : slugify(e.target.value),
                  adminNombre: editId ? form.adminNombre : (form.adminNombre || `Administrador ${e.target.value}`.trim()),
                })}
              />
            </div>
            <div className="form-group">
              <label>Slug (URL kiosk) *</label>
              <input className="form-control" value={form.slug} onChange={e => setForm({ ...form, slug: e.target.value.toLowerCase() })} placeholder="mi-empresa" />
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

            <hr style={{ margin: '1.25rem 0', border: 'none', borderTop: '1px solid var(--border)' }} />
            <p style={{ margin: '0 0 0.75rem', color: 'var(--text-muted)', fontSize: '0.9rem' }}>
              Funciones opcionales visibles para este emisor (no afectan datos existentes).
            </p>
            <label className="checkbox-label" style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: '0.5rem' }}>
              <input
                type="checkbox"
                checked={form.mostrarConfigPortero}
                onChange={e => setForm({ ...form, mostrarConfigPortero: e.target.checked })}
              />
              Mostrar config portero
            </label>
            <label className="checkbox-label" style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: '0.5rem' }}>
              <input
                type="checkbox"
                checked={form.mostrarSorteo}
                onChange={e => setForm({ ...form, mostrarSorteo: e.target.checked })}
              />
              Mostrar botón de sorteo
            </label>
            <label className="checkbox-label" style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: '0.5rem' }}>
              <input
                type="checkbox"
                checked={form.mostrarControlIngreso}
                onChange={e => setForm({ ...form, mostrarControlIngreso: e.target.checked })}
              />
              Mostrar Control de Ingreso
            </label>
            <label className="checkbox-label" style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: '0.75rem' }}>
              <input
                type="checkbox"
                checked={form.mostrarAvisoPagoPendiente}
                onChange={e => setForm({ ...form, mostrarAvisoPagoPendiente: e.target.checked })}
              />
              Mostrar aviso de pago pendiente al iniciar sesión
            </label>

            {!editId && (
              <>
                <hr style={{ margin: '1.25rem 0', border: 'none', borderTop: '1px solid var(--border)' }} />
                <p style={{ margin: '0 0 1rem', color: 'var(--text-muted)', fontSize: '0.9rem' }}>
                  Datos de acceso del administrador de este emisor. Con este usuario podrá gestionar sus socios de forma independiente.
                </p>
                <div className="form-group">
                  <label>Email del administrador *</label>
                  <input
                    type="email"
                    className="form-control"
                    value={form.adminEmail}
                    onChange={e => setForm({ ...form, adminEmail: e.target.value })}
                    placeholder="admin@mi-empresa.com"
                    autoComplete="off"
                  />
                </div>
                <div className="form-group">
                  <label>Nombre del administrador *</label>
                  <input
                    className="form-control"
                    value={form.adminNombre}
                    onChange={e => setForm({ ...form, adminNombre: e.target.value })}
                    placeholder="Juan Pérez"
                  />
                </div>
                <div className="form-row">
                  <div className="form-group">
                    <label>Contraseña *</label>
                    <input
                      type="password"
                      className="form-control"
                      value={form.adminPassword}
                      onChange={e => setForm({ ...form, adminPassword: e.target.value })}
                      autoComplete="new-password"
                    />
                  </div>
                  <div className="form-group">
                    <label>Confirmar contraseña *</label>
                    <input
                      type="password"
                      className="form-control"
                      value={form.adminPasswordConfirm}
                      onChange={e => setForm({ ...form, adminPasswordConfirm: e.target.value })}
                      autoComplete="new-password"
                    />
                  </div>
                </div>
              </>
            )}

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
