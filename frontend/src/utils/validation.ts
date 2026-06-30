export const LIMITS = {
  nombre: 50,
  apellido: 50,
  cedula: 20,
  documentoOtro: 50,
  telefono: 20,
  email: 100,
  notas: 500,
};

const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const cedulaUyRegex = /^\d{1,3}\.\d{3}\.\d{3}-\d$/;

export function isValidEmail(email: string): boolean {
  return emailRegex.test(email);
}

export function validateSocio(form: {
  nombre: string;
  apellido: string;
  cedula: string;
  tipoIdentificacion: 'Cedula' | 'Otro';
  email: string;
  fechaAlta: string;
  fechaVencimiento: string;
  cuotaMensual: number;
}): string[] {
  const errors: string[] = [];
  if (!form.nombre.trim()) errors.push('El nombre es obligatorio');
  else if (form.nombre.length > LIMITS.nombre) errors.push(`El nombre no puede superar ${LIMITS.nombre} caracteres`);
  if (!form.apellido.trim()) errors.push('El apellido es obligatorio');
  else if (form.apellido.length > LIMITS.apellido) errors.push(`El apellido no puede superar ${LIMITS.apellido} caracteres`);
  if (!form.cedula.trim()) {
    errors.push(form.tipoIdentificacion === 'Cedula' ? 'La cédula es obligatoria' : 'La identificación es obligatoria');
  } else if (form.tipoIdentificacion === 'Cedula') {
    if (!cedulaUyRegex.test(form.cedula.trim())) errors.push('La cédula debe tener el formato uruguayo X.XXX.XXX-X');
  } else if (form.cedula.trim().length > LIMITS.documentoOtro) {
    errors.push(`La identificación no puede superar ${LIMITS.documentoOtro} caracteres`);
  }
  if (!form.fechaAlta) errors.push('La fecha de alta es obligatoria');
  if (form.cuotaMensual <= 0) errors.push('La cuota mensual debe ser mayor a 0');
  if (form.email && !isValidEmail(form.email)) errors.push('El formato del email no es válido');
  if (form.fechaAlta && new Date(form.fechaAlta) > new Date()) errors.push('La fecha de alta no puede ser futura');
  if (form.fechaVencimiento && form.fechaAlta && new Date(form.fechaVencimiento) < new Date(form.fechaAlta))
    errors.push('La fecha de vencimiento debe ser posterior a la fecha de alta');
  return errors;
}

export function validateFamilia(form: { nombre: string; cuotaMensual: number }): string[] {
  const errors: string[] = [];
  if (!form.nombre.trim()) errors.push('El nombre de la familia es obligatorio');
  else if (form.nombre.length > LIMITS.nombre) errors.push(`El nombre no puede superar ${LIMITS.nombre} caracteres`);
  if (form.cuotaMensual <= 0) errors.push('La cuota mensual debe ser mayor a 0');
  return errors;
}

export function validateCliente(form: { nombre: string; apellido: string; email: string }): string[] {
  const errors: string[] = [];
  if (!form.nombre.trim()) errors.push('El nombre es obligatorio');
  if (!form.apellido.trim()) errors.push('El apellido es obligatorio');
  if (form.email && !isValidEmail(form.email)) errors.push('El formato del email no es válido');
  return errors;
}

export function validateServicio(form: { nombre: string; precio: number; duracionMinutos: number }): string[] {
  const errors: string[] = [];
  if (!form.nombre.trim()) errors.push('El nombre del servicio es obligatorio');
  if (form.precio <= 0) errors.push('El precio debe ser mayor a 0');
  if (form.duracionMinutos < 0) errors.push('La duración no puede ser negativa');
  return errors;
}

export async function parseApiError(e: unknown): Promise<string> {
  if (e instanceof Error) {
    try {
      const parsed = JSON.parse(e.message);
      if (parsed.errores?.length) return parsed.errores.join('. ');
      if (parsed.mensaje) return parsed.mensaje;
    } catch {
      return e.message;
    }
    return e.message;
  }
  return 'Error inesperado';
}
