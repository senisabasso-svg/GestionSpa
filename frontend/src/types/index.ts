export const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000/api';

export type EstadoSocio = 'Activo' | 'Suspendido' | 'Inactivo';
export type EstadoPago = 'Pendiente' | 'Pagado' | 'Parcial';
export type CategoriaServicio = 'Masajes' | 'Termal' | 'Facial' | 'Corporal' | 'Paquetes' | 'Otros';
export type MetodoPago = 'Efectivo' | 'TarjetaDebito' | 'TarjetaCredito' | 'Transferencia' | 'MercadoPago';
export type TipoIngreso = 'Entrada' | 'Salida';

export interface Socio {
  id: number;
  numeroSocio: string;
  nombre: string;
  apellido: string;
  cedula: string;
  telefono?: string;
  email?: string;
  direccion?: string;
  ciudad?: string;
  fechaAlta: string;
  fechaVencimiento?: string;
  medioPago: MetodoPago;
  cuotaMensual: number;
  estado: EstadoSocio;
  observaciones?: string;
}

export interface Cliente {
  id: number;
  nombre: string;
  apellido: string;
  cedula?: string;
  telefono?: string;
  email?: string;
  fechaRegistro: string;
}

export interface Servicio {
  id: number;
  nombre: string;
  descripcion?: string;
  categoria: CategoriaServicio;
  precio: number;
  duracionMinutos: number;
  activo: boolean;
  soloSocios: boolean;
}

export interface Cargo {
  id: number;
  servicioId: number;
  servicioNombre: string;
  socioId?: number;
  socioNombre?: string;
  clienteId?: number;
  clienteNombre?: string;
  fecha: string;
  monto: number;
  cantidad: number;
  estadoPago: EstadoPago;
  sumarACuota: boolean;
  notas?: string;
}

export interface CuotaMensual {
  id: number;
  socioId: number;
  numeroSocio: string;
  socioNombre: string;
  mes: number;
  anio: number;
  montoCuota: number;
  montoServicios: number;
  total: number;
  montoPagado: number;
  saldoPendiente: number;
  estadoPago: EstadoPago;
  fechaVencimiento?: string;
  fechaPago?: string;
}

export interface ResultadoIngreso {
  accesoPermitido: boolean;
  mensaje: string;
  nombreCompleto?: string;
  numeroSocio?: string;
  estado?: EstadoSocio;
  estadoCuota?: EstadoPago;
}

export interface InformeResumen {
  totalIngresosMes: number;
  totalPendiente: number;
  totalCobrado: number;
  sociosActivos: number;
  ingresosHoy: number;
  cuotasPendientes: number;
  cargosPendientes: number;
}

export interface InformeCobranza {
  socioId: number;
  numeroSocio: string;
  nombreCompleto: string;
  totalPendiente: number;
  totalPagado: number;
  estadoCuotaMes: EstadoPago;
  cargosPendientes: { id: number; servicio: string; monto: number; fecha: string; estado: EstadoPago }[];
}

export interface Ingreso {
  id: number;
  socioId: number;
  numeroSocio: string;
  socioNombre: string;
  fechaHora: string;
  tipo: TipoIngreso;
  accesoPermitido: boolean;
  motivoRechazo?: string;
}

export const METODOS_PAGO: { value: MetodoPago; label: string }[] = [
  { value: 'Efectivo', label: 'Efectivo' },
  { value: 'TarjetaDebito', label: 'Tarjeta de débito' },
  { value: 'TarjetaCredito', label: 'Tarjeta de crédito' },
  { value: 'Transferencia', label: 'Transferencia bancaria' },
  { value: 'MercadoPago', label: 'Mercado Pago' },
];

export const labelMetodoPago = (m: MetodoPago) =>
  METODOS_PAGO.find(x => x.value === m)?.label ?? m;

export const MESES = [
  'Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio',
  'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'
];

export const formatUYU = (monto: number) =>
  new Intl.NumberFormat('es-UY', { style: 'currency', currency: 'UYU', maximumFractionDigits: 0 }).format(monto);

export const formatFecha = (fecha: string) =>
  new Date(fecha).toLocaleDateString('es-UY', { day: '2-digit', month: '2-digit', year: 'numeric' });

export const formatHora = (fecha: string) =>
  new Date(fecha).toLocaleTimeString('es-UY', { hour: '2-digit', minute: '2-digit' });
