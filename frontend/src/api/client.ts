import { API_URL } from '../types';

async function request<T>(endpoint: string, options?: RequestInit): Promise<T> {
  const res = await fetch(`${API_URL}${endpoint}`, {
    headers: { 'Content-Type': 'application/json', ...options?.headers },
    ...options,
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({ mensaje: res.statusText }));
    throw new Error(err.mensaje || 'Error en la solicitud');
  }
  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}

export const api = {
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
    validar: (numeroSocio: string) => request<import('../types').ResultadoIngreso>('/ingresos/validar', { method: 'POST', body: JSON.stringify({ numeroSocio }) }),
    salida: (numeroSocio: string) => request<import('../types').Ingreso>('/ingresos/salida', { method: 'POST', body: JSON.stringify({ numeroSocio }) }),
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
  },
};
