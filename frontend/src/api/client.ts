import { API_URL } from '../types';

let authToken: string | null = null;
let emisorId: number | null = null;

export function setAuthToken(token: string | null) { authToken = token; }
export function setEmisorId(id: number | null) { emisorId = id; }

async function request<T>(endpoint: string, options?: RequestInit & { skipAuth?: boolean }): Promise<T> {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  if (!options?.skipAuth && authToken) headers['Authorization'] = `Bearer ${authToken}`;
  if (emisorId) headers['X-Emisor-Id'] = String(emisorId);

  const res = await fetch(`${API_URL}${endpoint}`, {
    ...options,
    headers: { ...headers, ...(options?.headers as Record<string, string>) },
  });
  if (res.status === 401) {
    localStorage.removeItem('gestionspa_auth');
    localStorage.removeItem('gestionspa_emisor');
    if (!endpoint.includes('/auth/login')) window.location.href = '/login';
  }
  if (!res.ok) {
    const err = await res.json().catch(() => ({ mensaje: res.statusText }));
    const msg = err.errores?.length ? err.errores.join('. ') : (err.mensaje || res.statusText);
    throw new Error(msg);
  }
  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}

async function downloadFile(endpoint: string, fallbackName: string) {
  const headers: Record<string, string> = {};
  if (authToken) headers['Authorization'] = `Bearer ${authToken}`;
  if (emisorId) headers['X-Emisor-Id'] = String(emisorId);

  const res = await fetch(`${API_URL}${endpoint}`, { headers });
  if (res.status === 401) {
    localStorage.removeItem('gestionspa_auth');
    localStorage.removeItem('gestionspa_emisor');
    window.location.href = '/login';
    throw new Error('Sesión expirada');
  }
  if (!res.ok) {
    const err = await res.json().catch(() => ({ mensaje: res.statusText }));
    throw new Error(err.mensaje || res.statusText);
  }
  const blob = await res.blob();
  const disposition = res.headers.get('Content-Disposition');
  const match = disposition?.match(/filename="?([^";\n]+)"?/);
  const name = match?.[1] || fallbackName;
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = name;
  a.click();
  URL.revokeObjectURL(url);
}

export const api = {
  auth: {
    login: (email: string, password: string) =>
      request<import('../types').LoginResponse>('/auth/login', {
        method: 'POST',
        body: JSON.stringify({ email, password }),
        skipAuth: true,
      }),
    me: () => request<import('../types').LoginResponse>('/auth/me'),
  },
  emisores: {
    list: () => request<import('../types').Emisor[]>('/emisores'),
    get: (id: number) => request<import('../types').Emisor>(`/emisores/${id}`),
    publico: (slug: string) =>
      request<import('../types').EmisorPublico>(`/emisores/publico/${slug}`, { skipAuth: true }),
    create: (data: import('../types').CrearEmisorPayload) =>
      request<import('../types').Emisor>('/emisores', { method: 'POST', body: JSON.stringify(data) }),
    update: (id: number, data: import('../types').ActualizarEmisorPayload) =>
      request<import('../types').Emisor>(`/emisores/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
    toggleActivo: (id: number, activo: boolean) =>
      request<import('../types').Emisor>(`/emisores/${id}/activo`, { method: 'PATCH', body: JSON.stringify(activo) }),
  },
  socios: {
    list: (buscar?: string, estado?: string) => {
      const p = new URLSearchParams();
      if (buscar) p.set('buscar', buscar);
      if (estado) p.set('estado', estado);
      const q = p.toString();
      return request<import('../types').Socio[]>(`/socios${q ? '?' + q : ''}`);
    },
    get: (id: number) => request<import('../types').Socio>(`/socios/${id}`),
    create: (data: unknown) => request<import('../types').Socio>('/socios', { method: 'POST', body: JSON.stringify(data) }),
    update: (id: number, data: unknown) => request<import('../types').Socio>(`/socios/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
    cambiarEstado: (id: number, estado: string) => request<import('../types').Socio>(`/socios/${id}/estado`, { method: 'PATCH', body: JSON.stringify(estado) }),
    delete: (id: number) => request<void>(`/socios/${id}`, { method: 'DELETE' }),
  },
  clientes: {
    list: (buscar?: string) => request<import('../types').Cliente[]>(`/clientes${buscar ? '?buscar=' + buscar : ''}`),
    create: (data: unknown) => request<import('../types').Cliente>('/clientes', { method: 'POST', body: JSON.stringify(data) }),
    update: (id: number, data: unknown) => request<import('../types').Cliente>(`/clientes/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
    delete: (id: number) => request<void>(`/clientes/${id}`, { method: 'DELETE' }),
  },
  familias: {
    list: (buscar?: string) => request<import('../types').Familia[]>(`/familias${buscar ? '?buscar=' + buscar : ''}`),
    get: (id: number) => request<import('../types').Familia>(`/familias/${id}`),
    create: (data: unknown) => request<import('../types').Familia>('/familias', { method: 'POST', body: JSON.stringify(data) }),
    update: (id: number, data: unknown) => request<import('../types').Familia>(`/familias/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
    delete: (id: number) => request<void>(`/familias/${id}`, { method: 'DELETE' }),
  },
  servicios: {
    list: (activos?: boolean) => request<import('../types').Servicio[]>(`/servicios${activos ? '?activos=true' : ''}`),
    create: (data: unknown) => request<import('../types').Servicio>('/servicios', { method: 'POST', body: JSON.stringify(data) }),
    update: (id: number, data: unknown) => request<import('../types').Servicio>(`/servicios/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
    toggle: (id: number) => request<import('../types').Servicio>(`/servicios/${id}/activo`, { method: 'PATCH' }),
  },
  cargos: {
    list: (params?: Record<string, string>) => {
      const q = params ? '?' + new URLSearchParams(params).toString() : '';
      return request<import('../types').Cargo[]>(`/cargos${q}`);
    },
    create: (data: unknown) => request<import('../types').Cargo>('/cargos', { method: 'POST', body: JSON.stringify(data) }),
    pagar: (id: number, data: unknown) => request<unknown>(`/cargos/${id}/pagar`, { method: 'POST', body: JSON.stringify(data) }),
    anular: (id: number, motivo?: string) => request<import('../types').Cargo>(`/cargos/${id}/anular`, { method: 'POST', body: JSON.stringify({ motivo: motivo || null }) }),
  },
  cuotas: {
    list: (mes?: number, anio?: number, estado?: string) => {
      const p = new URLSearchParams();
      if (mes) p.set('mes', String(mes));
      if (anio) p.set('anio', String(anio));
      if (estado) p.set('estado', estado);
      return request<import('../types').CuotaMensual[]>(`/cuotas?${p}`);
    },
    pagar: (id: number, data: unknown) => request<unknown>(`/cuotas/${id}/pagar`, { method: 'POST', body: JSON.stringify(data) }),
    generar: (mes?: number, anio?: number) => {
      const p = new URLSearchParams();
      if (mes) p.set('mes', String(mes));
      if (anio) p.set('anio', String(anio));
      return request<{ mensaje: string }>(`/cuotas/generar?${p}`, { method: 'POST' });
    },
  },
  ingresos: {
    list: (fecha?: string) => request<import('../types').Ingreso[]>(`/ingresos${fecha ? '?fecha=' + fecha : ''}`),
    validar: (numeroSocio: string, emisorSlug?: string) =>
      request<import('../types').ResultadoIngreso>('/ingresos/validar', {
        method: 'POST',
        body: JSON.stringify({ numeroSocio, emisorSlug }),
        skipAuth: true,
      }),
    salida: (numeroSocio: string, emisorSlug?: string) =>
      request<import('../types').Ingreso>('/ingresos/salida', {
        method: 'POST',
        body: JSON.stringify({ numeroSocio, emisorSlug }),
        skipAuth: true,
      }),
  },
  informes: {
    resumen: (mes?: number, anio?: number) => {
      const p = new URLSearchParams();
      if (mes) p.set('mes', String(mes));
      if (anio) p.set('anio', String(anio));
      return request<import('../types').InformeResumen>(`/informes/resumen?${p}`);
    },
    cobranza: (mes?: number, anio?: number) => {
      const p = new URLSearchParams();
      if (mes) p.set('mes', String(mes));
      if (anio) p.set('anio', String(anio));
      return request<import('../types').InformeCobranza[]>(`/informes/cobranza?${p}`);
    },
    ingresosDiarios: (fecha?: string) => request<{
      fecha: string; totalEntradas: number; accesosPermitidos: number;
      accesosRechazados: number; detalle: import('../types').Ingreso[];
    }>(`/informes/ingresos-diarios${fecha ? '?fecha=' + fecha : ''}`),
    pagos: () => request<unknown[]>('/informes/pagos'),
    serviciosMasVendidos: (mes?: number, anio?: number) => {
      const p = new URLSearchParams();
      if (mes) p.set('mes', String(mes));
      if (anio) p.set('anio', String(anio));
      return request<{ nombre: string; cantidad: number; total: number }[]>(`/informes/servicios-mas-vendidos?${p}`);
    },
    sociosActivos: (mes?: number, anio?: number) => {
      const p = new URLSearchParams();
      if (mes) p.set('mes', String(mes));
      if (anio) p.set('anio', String(anio));
      return request<import('../types').InformeSociosActivos>(`/informes/socios-activos?${p}`);
    },
    exportSociosActivos: (mes?: number, anio?: number) => {
      const p = new URLSearchParams();
      if (mes) p.set('mes', String(mes));
      if (anio) p.set('anio', String(anio));
      return downloadFile(`/informes/socios-activos/export?${p}`, 'socios-activos.csv');
    },
  },
};
