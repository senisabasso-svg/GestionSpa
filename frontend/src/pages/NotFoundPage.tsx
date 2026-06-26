import { Link } from 'react-router-dom';

export default function NotFoundPage() {
  return (
    <div style={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', background: 'var(--color-bg)', padding: '2rem' }}>
      <div className="card" style={{ textAlign: 'center', maxWidth: 420 }}>
        <div style={{ fontSize: '4rem', marginBottom: '1rem' }}>404</div>
        <h2 style={{ color: 'var(--color-primary-dark)', marginBottom: '0.5rem' }}>Página no encontrada</h2>
        <p style={{ color: 'var(--color-text-muted)', marginBottom: '1.5rem' }}>
          La ruta que buscás no existe en el sistema.
        </p>
        <Link to="/" className="btn btn-primary">Volver al Panel</Link>
      </div>
    </div>
  );
}
