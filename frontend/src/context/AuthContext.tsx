import { createContext, useContext, useState, useEffect, useCallback, type ReactNode } from 'react';
import type { LoginResponse, RolUsuario } from '../types';
import { api, setAuthToken, setEmisorId } from '../api/client';

interface AuthState {
  token: string;
  usuarioId: number;
  email: string;
  nombre: string;
  rol: RolUsuario;
  emisorId: number | null;
  emisorNombre: string | null;
  emisorSlug: string | null;
}

interface AuthContextValue extends AuthState {
  isAuthenticated: boolean;
  isSuperAdmin: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
  selectEmisor: (id: number, nombre: string, slug: string) => void;
  clearEmisorSelection: () => void;
  activeEmisorId: number | null;
}

const STORAGE_KEY = 'gestionspa_auth';

const AuthContext = createContext<AuthContextValue | null>(null);

function loadStored(): AuthState | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? JSON.parse(raw) as AuthState : null;
  } catch {
    return null;
  }
}

function saveStored(state: AuthState | null) {
  if (state) localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
  else localStorage.removeItem(STORAGE_KEY);
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [auth, setAuth] = useState<AuthState | null>(() => loadStored());
  const [selectedEmisorId, setSelectedEmisorId] = useState<number | null>(() => {
    const s = loadStored();
    return s?.rol === 'SuperAdmin' ? Number(localStorage.getItem('gestionspa_emisor') || '') || null : null;
  });

  useEffect(() => {
    if (auth) {
      setAuthToken(auth.token);
      const emisor = auth.rol === 'SuperAdmin' ? selectedEmisorId : auth.emisorId;
      setEmisorId(emisor);
    } else {
      setAuthToken(null);
      setEmisorId(null);
    }
  }, [auth, selectedEmisorId]);

  const login = useCallback(async (email: string, password: string) => {
    const res: LoginResponse = await api.auth.login(email, password);
    const state: AuthState = {
      token: res.token,
      usuarioId: res.usuarioId,
      email: res.email,
      nombre: res.nombre,
      rol: res.rol,
      emisorId: res.emisorId,
      emisorNombre: res.emisorNombre,
      emisorSlug: res.emisorSlug,
    };
    setAuth(state);
    saveStored(state);
    if (res.rol === 'SuperAdmin' && res.emisorId) {
      setSelectedEmisorId(res.emisorId);
      localStorage.setItem('gestionspa_emisor', String(res.emisorId));
    }
  }, []);

  const logout = useCallback(() => {
    setAuth(null);
    setSelectedEmisorId(null);
    saveStored(null);
    localStorage.removeItem('gestionspa_emisor');
  }, []);

  const selectEmisor = useCallback((id: number, nombre: string, slug: string) => {
    setSelectedEmisorId(id);
    localStorage.setItem('gestionspa_emisor', String(id));
    if (auth) {
      const updated = { ...auth, emisorNombre: nombre, emisorSlug: slug };
      setAuth(updated);
      saveStored(updated);
    }
  }, [auth]);

  const clearEmisorSelection = useCallback(() => {
    setSelectedEmisorId(null);
    localStorage.removeItem('gestionspa_emisor');
  }, []);

  const activeEmisorId = auth?.rol === 'SuperAdmin' ? selectedEmisorId : auth?.emisorId ?? null;

  const value: AuthContextValue = {
    token: auth?.token ?? '',
    usuarioId: auth?.usuarioId ?? 0,
    email: auth?.email ?? '',
    nombre: auth?.nombre ?? '',
    rol: auth?.rol ?? 'Operador',
    emisorId: auth?.emisorId ?? null,
    emisorNombre: auth?.emisorNombre ?? null,
    emisorSlug: auth?.emisorSlug ?? null,
    isAuthenticated: !!auth?.token,
    isSuperAdmin: auth?.rol === 'SuperAdmin',
    login,
    logout,
    selectEmisor,
    clearEmisorSelection,
    activeEmisorId,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth debe usarse dentro de AuthProvider');
  return ctx;
}
