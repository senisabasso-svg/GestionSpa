namespace GestionSpa.Api.Models;

/// <summary>
/// Snapshot de usuarios leídos del equipo ZKTeco al pulsar "Exportar socios de portero".
/// Solo se usa para ese flujo; no participa del cobro, ingreso ni sync operativo.
/// </summary>
public class PorteroUsuarioExtraido
{
    public long Id { get; set; }
    public int EmisorId { get; set; }
    public Emisor Emisor { get; set; } = null!;

    /// <summary>PIN / cédula en el dispositivo.</summary>
    public string Pin { get; set; } = "";

    public string Nombre { get; set; } = "";
    public int Privilegio { get; set; }
    public string Tarjeta { get; set; } = "";
    public string? DeviceSn { get; set; }

    public DateTime PrimeraExtraccionUtc { get; set; } = DateTime.UtcNow;
    public DateTime UltimaExtraccionUtc { get; set; } = DateTime.UtcNow;
}
