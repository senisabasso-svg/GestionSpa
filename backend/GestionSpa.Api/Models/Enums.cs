namespace GestionSpa.Api.Models;

public enum EstadoSocio
{
    Activo,
    Suspendido,
    Inactivo
}

public enum TipoIdentificacionSocio
{
    Cedula,
    Otro
}

public enum EstadoPago
{
    Pendiente,
    Pagado,
    Parcial,
    Anulado
}

public enum CategoriaServicio
{
    Masajes,
    Termal,
    Facial,
    Corporal,
    Paquetes,
    Otros
}

public enum MetodoPago
{
    Efectivo,
    TarjetaDebito,
    TarjetaCredito,
    Transferencia,
    MercadoPago
}

public enum TipoIngreso
{
    Entrada,
    Salida
}

public enum RolUsuario
{
    SuperAdmin,
    AdminEmisor,
    Operador
}
