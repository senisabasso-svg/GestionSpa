import { useEffect, useRef, useState } from 'react';
import { useAuth } from '../context/AuthContext';
import { AlertTriangle } from 'lucide-react';

const SESSION_KEY = 'gestionspa_aviso_pago_dismissed';
const INTERVALO_MS = 10 * 60 * 1000;

const MSG_INICIAL = 'Aviso de pago pendiente, requiere extensión servidor.';
const MSG_RECURRENTE = 'Último día de servidor, contactar a soporte.';

type Modo = 'inicial' | 'recurrente';

export default function AvisoPagoPendienteModal() {
  const { isAuthenticated, mostrarAvisoPagoPendiente, usuarioId, activeEmisorId } = useAuth();
  const [visible, setVisible] = useState(false);
  const [modo, setModo] = useState<Modo>('inicial');
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const sessionKey = `${SESSION_KEY}:${usuarioId}:${activeEmisorId ?? 'none'}`;

  const clearTimer = () => {
    if (timerRef.current) {
      clearInterval(timerRef.current);
      timerRef.current = null;
    }
  };

  const startRecurring = () => {
    clearTimer();
    timerRef.current = setInterval(() => {
      setModo('recurrente');
      setVisible(true);
    }, INTERVALO_MS);
  };

  useEffect(() => {
    clearTimer();
    if (!isAuthenticated || !mostrarAvisoPagoPendiente) {
      setVisible(false);
      return;
    }

    if (sessionStorage.getItem(sessionKey) === '1') {
      setVisible(false);
      startRecurring();
      return;
    }

    setModo('inicial');
    setVisible(true);
    return clearTimer;
  }, [isAuthenticated, mostrarAvisoPagoPendiente, usuarioId, activeEmisorId, sessionKey]);

  useEffect(() => () => clearTimer(), []);

  if (!visible) return null;

  const cerrar = () => {
    sessionStorage.setItem(sessionKey, '1');
    setVisible(false);
    startRecurring();
  };

  const mensaje = modo === 'inicial' ? MSG_INICIAL : MSG_RECURRENTE;

  return (
    <div className="modal-overlay" role="dialog" aria-modal="true" aria-labelledby="aviso-pago-titulo">
      <div className="modal" style={{ maxWidth: 420 }} onClick={e => e.stopPropagation()}>
        <div style={{ display: 'flex', alignItems: 'flex-start', gap: '0.75rem', marginBottom: '1rem' }}>
          <AlertTriangle size={28} style={{ color: 'var(--color-warning, #c9a227)', flexShrink: 0, marginTop: 2 }} />
          <div>
            <h3 id="aviso-pago-titulo" style={{ margin: '0 0 0.5rem' }}>Aviso</h3>
            <p style={{ margin: 0, lineHeight: 1.45 }}>{mensaje}</p>
          </div>
        </div>
        <div className="modal-actions">
          <button type="button" className="btn btn-primary" onClick={cerrar}>
            Entendido
          </button>
        </div>
      </div>
    </div>
  );
}
