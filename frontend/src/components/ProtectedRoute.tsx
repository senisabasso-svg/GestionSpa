import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

export default function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isSuperAdmin, activeEmisorId } = useAuth();
  const location = useLocation();

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  if (isSuperAdmin && !activeEmisorId && location.pathname !== '/emisores') {
    return <Navigate to="/emisores" replace />;
  }

  return <>{children}</>;
}
