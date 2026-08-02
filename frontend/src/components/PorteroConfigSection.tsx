import { useEffect, useState } from 'react';
import { api } from '../api/client';
import type { GuardarPorteroConfig, PorteroConfig, PorteroPruebaConexion, PorteroSincronizacion } from '../types';
import { useAuth } from '../context/AuthContext';
import { Wifi, RefreshCw, DoorOpen, Copy, Check } from 'lucide-react';

const emptyForm: GuardarPorteroConfig = {
  habilitado: false,
  apiUrl: 'pull',
  apiKey: '',
  webhookSecret: '',
  deviceSn: '7674222960189',
  sincronizarAutomatico: true,
};

export default function PorteroConfigSection({ mostrarConfigCompleta = true }: { mostrarConfigCompleta?: boolean }) {
  const { emisorSlug } = useAuth();
  const [form, setForm] = useState<GuardarPorteroConfig>(emptyForm);
  const [webhookUrl, setWebhookUrl] = useState('');
  const [agentPullUrl, setAgentPullUrl] = useState('');
  const [ultimoHeartbeat, setUltimoHeartbeat] = useState<string | null>(null);
  const [comandosPendientes, setComandosPendientes] = useState(0);
  const [fechaActualizacion, setFechaActualizacion] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [probando, setProbando] = useState(false);
  const [sincronizando, setSincronizando] = useState(false);
  const [abriendo, setAbriendo] = useState(false);
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [prueba, setPrueba] = useState<PorteroPruebaConexion | null>(null);
  const [syncResult, setSyncResult] = useState<PorteroSincronizacion | null>(null);
  const [copied, setCopied] = useState<string | null>(null);

  const applyConfig = (cfg: PorteroConfig) => {
    setForm({
      habilitado: cfg.habilitado,
      apiUrl: cfg.apiUrl || 'pull',
      apiKey: cfg.apiKey,
      webhookSecret: cfg.webhookSecret || '',
      deviceSn: cfg.deviceSn,
      sincronizarAutomatico: cfg.sincronizarAutomatico,
    });
    setWebhookUrl(cfg.webhookUrl);
    setAgentPullUrl(cfg.agentPullUrl);
    setUltimoHeartbeat(cfg.ultimoHeartbeatUtc);
    setComandosPendientes(cfg.comandosPendientes ?? 0);
    setFechaActualizacion(cfg.fechaActualizacion);
  };

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      applyConfig(await api.portero.getConfig());
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Error al cargar configuración');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  const save = async () => {
    setSaving(true);
    setError(null);
    setMensaje(null);
    try {
      const cfg = await api.portero.saveConfig({
        ...form,
        apiUrl: form.apiUrl?.trim() || 'pull',
        webhookSecret: form.webhookSecret?.trim() || null,
      });
      applyConfig(cfg);
      setMensaje('Configuración guardada. En la PC del spa usá la misma API Key y la URL base de esta API.');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Error al guardar');
    } finally {
      setSaving(false);
    }
  };

  const probar = async () => {
    setProbando(true);
    setError(null);
    setPrueba(null);
    try {
      setPrueba(await api.portero.probar());
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Error al probar conexión');
    } finally {
      setProbando(false);
    }
  };

  const sincronizar = async () => {
    setSincronizando(true);
    setError(null);
    setSyncResult(null);
    try {
      const result = await api.portero.sincronizar();
      setSyncResult(result);
      setMensaje(`Encolados ${result.exitosos}/${result.total} socios. El agente en la PC los aplicará en segundos.`);
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Error al sincronizar');
    } finally {
      setSincronizando(false);
    }
  };

  const abrirPuerta = async () => {
    setAbriendo(true);
    setError(null);
    try {
      setMensaje((await api.portero.abrirPuerta()).mensaje);
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Error al abrir puerta');
    } finally {
      setAbriendo(false);
    }
  };

  const copyText = async (value: string, key: string) => {
    if (!value) return;
    await navigator.clipboard.writeText(value);
    setCopied(key);
    setTimeout(() => setCopied(null), 2000);
  };

  if (loading) {
    if (!mostrarConfigCompleta) return null;
    return <div className="loading">Cargando configuración del portero...</div>;
  }

  // Sin config completa: solo acciones, y solo si el portero ya está habilitado.
  if (!mostrarConfigCompleta && !form.habilitado) return null;

  const acciones = (
    <div className="card">
      <h3>{mostrarConfigCompleta ? 'Acciones' : 'Portero biométrico'}</h3>
      {!mostrarConfigCompleta && (
        <p className="text-muted" style={{ marginBottom: '0.75rem' }}>
          Sincronizá socios o abrí la puerta. La configuración avanzada la gestiona el administrador del sistema.
        </p>
      )}
      <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', marginTop: mostrarConfigCompleta ? '0.75rem' : 0 }}>
        <button type="button" className="btn btn-secondary" onClick={sincronizar} disabled={sincronizando || !form.habilitado}>
          <RefreshCw size={16} /> {sincronizando ? 'Encolando...' : 'Sincronizar socios activos'}
        </button>
        <button type="button" className="btn btn-secondary" onClick={abrirPuerta} disabled={abriendo || !form.habilitado}>
          <DoorOpen size={16} /> {abriendo ? 'Encolando...' : 'Abrir puerta'}
        </button>
      </div>
      {syncResult && syncResult.errores.length > 0 && (
        <div className="alert alert-error" style={{ marginTop: '1rem' }}>
          <strong>Errores ({syncResult.fallidos}):</strong>
          <ul style={{ margin: '0.5rem 0 0', paddingLeft: '1.25rem' }}>{syncResult.errores.map((e, i) => <li key={i}>{e}</li>)}</ul>
        </div>
      )}
    </div>
  );

  if (!mostrarConfigCompleta) {
    return (
      <section id="portero">
        {mensaje && <div className="alert alert-success">{mensaje}</div>}
        {error && <div className="alert alert-error">{error}</div>}
        {acciones}
      </section>
    );
  }

  return (
    <section id="portero">
      {mensaje && <div className="alert alert-success">{mensaje}</div>}
      {error && <div className="alert alert-error">{error}</div>}

      <div className="card" style={{ marginBottom: '1.5rem' }}>
        <h3>Portero biométrico (modo pull)</h3>
        <p className="text-muted" style={{ marginBottom: '1rem' }}>
          GestionSpa encola altas, bajas y abrir puerta. ApiPorteroSpa (Railway o PC) consulta sola esta API cada N s.
          «Probar agente» no llama al portero: solo mira si llegó un heartbeat reciente a esta base.
        </p>

        <label className="checkbox-label" style={{ marginBottom: '1rem', display: 'flex', alignItems: 'center', gap: 8 }}>
          <input type="checkbox" checked={form.habilitado} onChange={e => setForm(f => ({ ...f, habilitado: e.target.checked }))} />
          Habilitar integración con portero
        </label>

        <div className="form-grid">
          <div className="form-group">
            <label>API Key (la misma en la PC del spa)</label>
            <input type="password" value={form.apiKey} onChange={e => setForm(f => ({ ...f, apiKey: e.target.value }))} autoComplete="off" />
          </div>
          <div className="form-group">
            <label>Serial del dispositivo (SN)</label>
            <input type="text" value={form.deviceSn} onChange={e => setForm(f => ({ ...f, deviceSn: e.target.value }))} />
          </div>
          <div className="form-group">
            <label>Secreto webhook (opcional)</label>
            <input type="password" value={form.webhookSecret ?? ''} onChange={e => setForm(f => ({ ...f, webhookSecret: e.target.value }))} autoComplete="off" />
          </div>
        </div>

        <label className="checkbox-label" style={{ marginTop: '1rem', display: 'flex', alignItems: 'center', gap: 8 }}>
          <input type="checkbox" checked={form.sincronizarAutomatico} onChange={e => setForm(f => ({ ...f, sincronizarAutomatico: e.target.checked }))} />
          Encolar sync automático al crear, editar o dar de baja socios
        </label>

        <div className="form-actions" style={{ marginTop: '1.25rem', display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
          <button type="button" className="btn btn-primary" onClick={save} disabled={saving}>{saving ? 'Guardando...' : 'Guardar'}</button>
          <button type="button" className="btn btn-secondary" onClick={probar} disabled={probando}><Wifi size={16} /> {probando ? 'Probando...' : 'Probar agente'}</button>
        </div>

        {prueba && <div className={`alert ${prueba.ok ? 'alert-success' : 'alert-error'}`} style={{ marginTop: '1rem' }}>{prueba.mensaje}</div>}
        <p className="text-muted" style={{ marginTop: '0.75rem', fontSize: '0.85rem' }}>
          Comandos pendientes: <strong>{comandosPendientes}</strong>
          {ultimoHeartbeat ? ` · Último heartbeat: ${new Date(ultimoHeartbeat).toLocaleString('es-UY')}` : ' · Sin heartbeat aún'}
          {fechaActualizacion ? ` · Config: ${new Date(fechaActualizacion).toLocaleString('es-UY')}` : ''}
        </p>
      </div>

      <div className="card" style={{ marginBottom: '1.5rem' }}>
        <h3>URLs para la PC del spa</h3>
        <p className="text-muted" style={{ marginBottom: '0.75rem' }}>
          En el panel ApiPorteroSpa: URL base = origen de esta API (sin path), slug = <strong>{emisorSlug || '…'}</strong>, misma API Key.
        </p>
        <label className="text-muted" style={{ fontSize: '0.85rem' }}>Webhook fichajes (auto si usás base + slug)</label>
        <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', marginTop: '0.35rem' }}>
          <input type="text" readOnly value={webhookUrl} style={{ flex: 1, fontFamily: 'monospace', fontSize: '0.85rem' }} />
          <button type="button" className="btn btn-sm btn-secondary" onClick={() => copyText(webhookUrl, 'wh')}>{copied === 'wh' ? <Check size={14} /> : <Copy size={14} />}</button>
        </div>
        <label className="text-muted" style={{ fontSize: '0.85rem', marginTop: '0.75rem', display: 'block' }}>Endpoint agente (pull)</label>
        <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', marginTop: '0.35rem' }}>
          <input type="text" readOnly value={agentPullUrl} style={{ flex: 1, fontFamily: 'monospace', fontSize: '0.85rem' }} />
          <button type="button" className="btn btn-sm btn-secondary" onClick={() => copyText(agentPullUrl, 'ag')}>{copied === 'ag' ? <Check size={14} /> : <Copy size={14} />}</button>
        </div>
      </div>

      {acciones}
    </section>
  );
}
