using GestionSpa.Api.Models;

namespace GestionSpa.Api.DTOs;

public record SocioDto(
    int Id, string NumeroSocio, string Nombre, string Apellido, string Cedula,
    string? Telefono, string? Email, string? Direccion, string? Ciudad,
    DateTime FechaAlta, DateTime? FechaVencimiento, MetodoPago MedioPago,
    decimal CuotaMensual, EstadoSocio Estado, string? Observaciones);

public record CrearSocioDto(
    string Nombre, string Apellido, string Cedula,
    string? Telefono, string? Email,
    DateTime FechaAlta, DateTime? FechaVencimiento,
    MetodoPago MedioPago, decimal CuotaMensual);

public record ClienteDto(
    int Id, string Nombre, string Apellido, string? Cedula,
    string? Telefono, string? Email, DateTime FechaRegistro);

public record CrearClienteDto(
    string Nombre, string Apellido, string? Cedula,
    string? Telefono, string? Email, string? Observaciones);

public record ServicioDto(
    int Id, string Nombre, string? Descripcion, CategoriaServicio Categoria,
    decimal Precio, int DuracionMinutos, bool Activo, bool SoloSocios);

public record CrearServicioDto(
    string Nombre, string? Descripcion, CategoriaServicio Categoria,
    decimal Precio, int DuracionMinutos, bool SoloSocios);

public record CargoDto(
    int Id, int ServicioId, string ServicioNombre, int? SocioId, string? SocioNombre,
    int? ClienteId, string? ClienteNombre, DateTime Fecha, decimal Monto,
    int Cantidad, EstadoPago EstadoPago, bool SumarACuota, string? Notas);

public record CrearCargoDto(
    int ServicioId, int? SocioId, int? ClienteId, int Cantidad,
    bool SumarACuota, string? Notas, string? AtendidoPor);

public record CuotaMensualDto(
    int Id, int SocioId, string NumeroSocio, string SocioNombre,
    int Mes, int Anio, decimal MontoCuota, decimal MontoServicios,
    decimal Total, decimal MontoPagado, decimal SaldoPendiente,
    EstadoPago EstadoPago, DateTime? FechaVencimiento, DateTime? FechaPago);

public record RegistrarPagoDto(
    decimal Monto, MetodoPago MetodoPago, string? Referencia,
    string? RegistradoPor, string? Notas, int? CargoId, int? CuotaMensualId);

public record PagoDto(
    int Id, decimal Monto, MetodoPago MetodoPago, DateTime Fecha,
    string? Referencia, string? RegistradoPor, int? CargoId, int? CuotaMensualId);

public record IngresoDto(
    int Id, int SocioId, string NumeroSocio, string SocioNombre,
    DateTime FechaHora, TipoIngreso Tipo, bool AccesoPermitido, string? MotivoRechazo);

public record ValidarIngresoDto(string NumeroSocio);

public record ResultadoIngresoDto(
    bool AccesoPermitido, string Mensaje, string? NombreCompleto,
    string? NumeroSocio, EstadoSocio? Estado, EstadoPago? EstadoCuota);

public record InformeResumenDto(
    decimal TotalIngresosMes, decimal TotalPendiente, decimal TotalCobrado,
    int SociosActivos, int IngresosHoy, int CuotasPendientes, int CargosPendientes);

public record InformeCobranzaDto(
    int SocioId, string NumeroSocio, string NombreCompleto,
    decimal TotalPendiente, decimal TotalPagado, EstadoPago EstadoCuotaMes,
    List<CargoPendienteDto> CargosPendientes);

public record CargoPendienteDto(
    int Id, string Servicio, decimal Monto, DateTime Fecha, EstadoPago Estado);

public record InformeIngresosDto(
    DateTime Fecha, int TotalEntradas, int AccesosPermitidos, int AccesosRechazados,
    List<IngresoDto> Detalle);
