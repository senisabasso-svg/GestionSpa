import { useState, useRef, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import type { ResultadoIngreso } from '../types';

const MAX_DIGITOS = 4;

export default function IngresoPage() {
  const [numero, setNumero] = useState('');
  const [resultado, setResultado] = useState<ResultadoIngreso | null>(null);
  const [errorMsg, setErrorMsg] = useState('');
  const [loading, setLoading] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => { inputRef.current?.focus(); }, []);

  const validar = async (n: string) => {
    if (!n.trim()) {
      setErrorMsg('Ingresá un número de socio');
      setResultado(null);
      return;
    }
    if (n.length !== MAX_DIGITOS) {
      setErrorMsg('Debés ingresar exactamente 4 dígitos');
      setResultado(null);
      return;
    }
    setErrorMsg('');
    setLoading(true);
    setResultado(null);
    try {
      const res = await api.ingresos.validar(n.trim());
      setResultado(res);
      setTimeout(() => { setNumero(''); setResultado(null); inputRef.current?.focus(); }, 4000);
    } catch {
      setResultado({ accesoPermitido: false, mensaje: 'Error de conexión con el servidor' });
    } finally {
      setLoading(false);
    }
  };

  const pressKey = (key: string) => {
    if (key === 'C') { setNumero(''); setResultado(null); setErrorMsg(''); return; }
    if (key === 'OK') { validar(numero); return; }
    if (key === 'DEL') { setNumero(prev => prev.slice(0, -1)); return; }
    if (numero.length < MAX_DIGITOS) { setNumero(prev => prev + key); setErrorMsg(''); }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') validar(numero);
  };

  return (
    <div className="kiosk-container">
      <Link to="/" className="kiosk-back-link">← Volver al panel</Link>

      <div className="kiosk-card">
        <div className="kiosk-logo">♨️</div>
        <div className="kiosk-title">SPA Thermal Daymán</div>
        <div className="kiosk-subtitle">Termas del Daymán · Salto, Uruguay</div>

        <p style={{ marginBottom: '1rem', color: 'var(--color-text-muted)', fontSize: '0.95rem' }}>
          Ingresá tu número de socio (4 dígitos)
        </p>

        <input
          ref={inputRef}
          className="kiosk-input"
          value={numero}
          onChange={e => {
            setErrorMsg('');
            setNumero(e.target.value.replace(/\D/g, '').slice(0, MAX_DIGITOS));
          }}
          onKeyDown={handleKeyDown}
          placeholder="----"
          maxLength={MAX_DIGITOS}
          inputMode="numeric"
          autoComplete="off"
        />

        {errorMsg && (
          <div className="kiosk-result error" style={{ marginTop: '1rem' }}>
            {errorMsg}
          </div>
        )}

        <div className="kiosk-keypad">
          {['1','2','3','4','5','6','7','8','9','C','0','OK'].map(key => (
            <button
              key={key}
              className={`kiosk-key${key === 'OK' ? ' action' : ''}${key === 'C' ? ' action' : ''}`}
              onClick={() => pressKey(key)}
              disabled={loading || (key !== 'OK' && key !== 'C' && key !== 'DEL' && numero.length >= MAX_DIGITOS)}
            >
              {key === 'OK' ? '✓' : key === 'C' ? '✕' : key}
            </button>
          ))}
        </div>

        {loading && <p style={{ marginTop: '1.5rem', color: 'var(--color-text-muted)' }}>Verificando...</p>}

        {resultado && (
          <div className={`kiosk-result ${resultado.accesoPermitido ? 'success' : 'error'}`}>
            <div style={{ fontSize: '2rem', marginBottom: '0.5rem' }}>
              {resultado.accesoPermitido ? '✅' : '❌'}
            </div>
            <strong>{resultado.mensaje}</strong>
            {resultado.nombreCompleto && (
              <div style={{ marginTop: '0.5rem', fontSize: '0.95rem' }}>{resultado.nombreCompleto}</div>
            )}
            {!resultado.accesoPermitido && resultado.estadoCuota === 'Pendiente' && (
              <div style={{ marginTop: '0.5rem', fontSize: '0.85rem' }}>
                Por favor, regularizá tu cuota en recepción.
              </div>
            )}
          </div>
        )}
      </div>

      <p style={{ color: 'rgba(255,255,255,0.6)', marginTop: '2rem', fontSize: '0.8rem' }}>
        Acceso a 8 piscinas termales · Sauna · Gimnasio · Hidromasajes
      </p>
    </div>
  );
}
