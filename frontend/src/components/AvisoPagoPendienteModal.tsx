import { useEffect, useState } from 'react';
import { useAuth } from '../context/AuthContext';
import { AlertTriangle } from 'lucide-react';

const SESSION_KEY = 'gestionspa_aviso_pago_dismissed';

export default function AvisoPagoPendienteModal() {
  const { isAuthenticated, mostrarAvisoPagoPendiente, usuarioId, activeEmisorId } = useAuth();
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    if (!isAuthenticated || !mostrarAvisoPagoPendiente) {
      setVisible(false);
      return;
    }
    const key = `${SESSION_KEY}:${usuarioId}:${activeEmisorId ?? 'none'}`;
    if (sessionStorage.getItem(key) === '1') {
      setVisible(false);
      return;
    }
    setVisible(true);
  }, [isAuthenticated, mostrarAvisoPagoPendiente, usuarioId, activeEmisorId]);

  if (!visible) return null;

  const cerrar = () => {
    const key = `${SESSION_KEY}:${usuarioId}:${activeEmisorId ?? 'none'}`;
    sessionStorage.setItem(key, '1');
    setVisible(false);
  };

  return (
    <div className="modal-overlay" role="dialog" aria-modal="true" aria-labelledby="aviso-pago-titulo">
      <div className="modal" style={{ maxWidth: 420 }} onClick={e => e.stopPropagation()}>
        <div style={{ display: 'flex', alignItems: 'flex-start', gap: '0.75rem', marginBottom: '1rem' }}>
          <AlertTriangle size={28} style={{ color: 'var(--color-warning, #c9a227)', flexShrink: 0, marginTop: 2 }} />
          <div>
            <h3 id="aviso-pago-titulo" style={{ margin: '0 0 0.5rem' }}>Aviso</h3>
            <p style={{ margin: 0, lineHeight: 1.45 }}>
              Aviso de pago pendiente, requiere extensión servidor.
            </p>
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
