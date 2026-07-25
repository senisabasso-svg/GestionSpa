namespace GestionSpa.Api.Models;

public enum PorteroComandoTipo
{
    UpsertSocio = 1,
    DeleteSocio = 2,
    AbrirPuerta = 3,
}

public enum PorteroComandoEstado
{
    Pendiente = 0,
    Procesando = 1,
    Hecho = 2,
    Error = 3,
}

public class PorteroComando
{
    public long Id { get; set; }
    public int EmisorId { get; set; }
    public Emisor Emisor { get; set; } = null!;
    public PorteroComandoTipo Tipo { get; set; }
    public string? ClaveIdempotencia { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public PorteroComandoEstado Estado { get; set; } = PorteroComandoEstado.Pendiente;
    public string? UltimoError { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaProcesado { get; set; }
}
