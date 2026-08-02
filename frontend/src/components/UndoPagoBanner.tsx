import { useEffect, useRef, useState } from 'react';
import { api } from '../api/client';
import { formatUYU } from '../types';
import type { PagoRegistrado } from '../types';
import { Undo2 } from 'lucide-react';

type Props = {
  pago: PagoRegistrado | null;
  etiqueta?: string;
  onDismiss: () => void;
  onReverted: () => void;
};

export default function UndoPagoBanner({ pago, etiqueta, onDismiss, onReverted }: Props) {
  const [segundos, setSegundos] = useState(10);
  const [revirtiendo, setRevirtiendo] = useState(false);
  const [error, setError] = useState('');
  const idsRef = useRef<number[]>([]);

  useEffect(() => {
    if (!pago) return;
    idsRef.current = pago.idsRevertibles?.length ? pago.idsRevertibles : [pago.id];
    const total = pago.segundosParaRevertir > 0 ? pago.segundosParaRevertir : 10;
    setSegundos(total);
    setError('');
    setRevirtiendo(false);

    const started = Date.now();
    const tick = window.setInterval(() => {
      const left = Math.max(0, total - Math.floor((Date.now() - started) / 1000));
      setSegundos(left);
      if (left <= 0) {
        window.clearInterval(tick);
        onDismiss();
      }
    }, 250);

    return () => window.clearInterval(tick);
  }, [pago, onDismiss]);

  if (!pago) return null;

  const revertir = async () => {
    setRevirtiendo(true);
    setError('');
    try {
      await api.pagos.revertir(idsRef.current);
      onReverted();
      onDismiss();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo revertir');
      setRevirtiendo(false);
    }
  };

  return (
    <div className="alert alert-success" style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', flexWrap: 'wrap' }}>
      <span style={{ flex: 1 }}>
        Pago registrado{etiqueta ? ` — ${etiqueta}` : ''}: <strong>{formatUYU(pago.monto)}</strong>
        {segundos > 0 ? ` · Podés revertir durante ${segundos}s` : ''}
      </span>
      <button
        type="button"
        className="btn btn-sm btn-secondary"
        onClick={revertir}
        disabled={revirtiendo || segundos <= 0}
      >
        <Undo2 size={14} /> {revirtiendo ? 'Revirtiendo…' : `Revertir pago (${segundos}s)`}
      </button>
      <button type="button" className="btn btn-sm btn-secondary" onClick={onDismiss} disabled={revirtiendo}>
        Cerrar
      </button>
      {error && <div className="alert alert-error" style={{ width: '100%', margin: 0 }}>{error}</div>}
    </div>
  );
}
