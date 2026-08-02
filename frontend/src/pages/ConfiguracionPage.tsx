import { useEffect, useState } from 'react';
import { Navigate } from 'react-router-dom';
import { api } from '../api/client';
import type { EmisorBackupResumen, EmisorImportResult } from '../types';
import { useAuth } from '../context/AuthContext';
import PorteroConfigSection from '../components/PorteroConfigSection';
import { Settings, Download, Upload, AlertTriangle } from 'lucide-react';

export default function ConfiguracionPage() {
  const { isSuperAdmin, emisorSlug, mostrarConfigPortero } = useAuth();
  if (isSuperAdmin) return <Navigate to="/" replace />;

  const [exportando, setExportando] = useState(false);
  const [importando, setImportando] = useState(false);
  const [resumen, setResumen] = useState<EmisorBackupResumen | null>(null);
  const [archivo, setArchivo] = useState<File | null>(null);
  const [confirmar, setConfirmar] = useState(false);
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [resultado, setResultado] = useState<EmisorImportResult | null>(null);

  const exportar = async () => {
    setExportando(true);
    setError(null);
    try {
      await api.configuracion.exportBackup();
      setMensaje('Respaldo exportado correctamente');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Error al exportar');
    } finally {
      setExportando(false);
    }
  };

  const cargarResumen = async () => {
    try {
      setResumen(await api.configuracion.resumenBackup());
    } catch {
      setResumen(null);
    }
  };

  useEffect(() => { cargarResumen(); }, []);

  const importar = async () => {
    if (!archivo) {
      setError('Seleccioná un archivo JSON de respaldo');
      return;
    }
    if (!confirmar) {
      setError('Debés confirmar que querés reemplazar todos los datos');
      return;
    }
    setImportando(true);
    setError(null);
    setMensaje(null);
    setResultado(null);
    try {
      const result = await api.configuracion.importBackup(archivo);
      setResultado(result);
      setMensaje(result.mensaje);
      setArchivo(null);
      setConfirmar(false);
      await cargarResumen();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Error al importar');
    } finally {
      setImportando(false);
    }
  };

  return (
    <div>
      <div className="page-header">
        <h2><Settings size={22} style={{ verticalAlign: 'middle', marginRight: 8 }} />Configuración</h2>
        <p className="text-muted">Respaldo de datos y conexión con el portero biométrico{emisorSlug ? ` — /${emisorSlug}` : ''}.</p>
      </div>

      {mensaje && <div className="alert alert-success">{mensaje}</div>}
      {error && <div className="alert alert-error">{error}</div>}

      <div className="card" style={{ marginBottom: '2rem' }}>
        <h3>Respaldo de datos de la empresa</h3>
        <p className="text-muted" style={{ marginBottom: '1rem' }}>
          Exportá un archivo JSON con todos los datos del spa (socios, cuotas, cargos, pagos, ingresos, usuarios y configuración del portero).
          Podés importarlo en otro servidor para replicar la misma información.
        </p>

        {resumen && (
          <div style={{ marginBottom: '1rem', fontSize: '0.9rem', lineHeight: 1.7 }}>
            <strong>{resumen.emisorNombre}</strong> ({resumen.emisorSlug})<br />
            Socios: {resumen.socios} · Clientes: {resumen.clientes} · Cuotas: {resumen.cuotas} · Cargos: {resumen.cargos} · Pagos: {resumen.pagos} · Ingresos: {resumen.ingresos}
          </div>
        )}

        <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', marginBottom: '1.5rem' }}>
          <button type="button" className="btn btn-primary" onClick={exportar} disabled={exportando}>
            <Download size={16} /> {exportando ? 'Exportando...' : 'Exportar respaldo'}
          </button>
        </div>

        <hr style={{ margin: '1.5rem 0', border: 'none', borderTop: '1px solid var(--border, #e0e0e0)' }} />

        <h4 style={{ marginTop: 0 }}>Importar respaldo</h4>
        <div className="alert alert-error" style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-start' }}>
          <AlertTriangle size={18} style={{ flexShrink: 0, marginTop: 2 }} />
          <span>La importación <strong>reemplaza todos los datos</strong> de esta empresa en el servidor actual (socios, cuotas, usuarios, etc.).</span>
        </div>

        <div className="form-group" style={{ marginTop: '1rem' }}>
          <label>Archivo JSON</label>
          <input
            type="file"
            accept=".json,application/json"
            onChange={e => { setArchivo(e.target.files?.[0] ?? null); setResultado(null); }}
          />
        </div>

        <label className="checkbox-label" style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: '0.75rem' }}>
          <input type="checkbox" checked={confirmar} onChange={e => setConfirmar(e.target.checked)} />
          Confirmo que quiero reemplazar todos los datos actuales con el respaldo
        </label>

        <div style={{ marginTop: '1rem' }}>
          <button type="button" className="btn btn-secondary" onClick={importar} disabled={importando || !archivo}>
            <Upload size={16} /> {importando ? 'Importando...' : 'Importar respaldo'}
          </button>
        </div>

        {resultado && (
          <div className="alert alert-success" style={{ marginTop: '1rem' }}>
            Importados: {resultado.socios} socios, {resultado.cuotas} cuotas, {resultado.cargos} cargos, {resultado.pagos} pagos, {resultado.ingresos} ingresos, {resultado.usuarios} usuarios.
          </div>
        )}
      </div>

      <PorteroConfigSection mostrarConfigCompleta={mostrarConfigPortero} />
    </div>
  );
}
