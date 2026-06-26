namespace GestionSpa.Api.Models;

public enum EstadoSocio
{
    Activo,
    Suspendido,
    Inactivo
}

public enum EstadoPago
{
    Pendiente,
    Pagado,
    Parcial
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
