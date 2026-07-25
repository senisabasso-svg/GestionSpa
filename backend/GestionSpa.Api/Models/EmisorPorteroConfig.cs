namespace GestionSpa.Api.Models;

public class EmisorPorteroConfig
{
    public int EmisorId { get; set; }
    public Emisor Emisor { get; set; } = null!;
    public bool Habilitado { get; set; }
    public string ApiUrl { get; set; } = "http://localhost:5000";
    public string ApiKey { get; set; } = "";
    public string? WebhookSecret { get; set; }
    public string DeviceSn { get; set; } = "7674222960189";
    public bool SincronizarAutomatico { get; set; } = true;
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;
    /// <summary>Último heartbeat del agente ApiPorteroSpa (pull).</summary>
    public DateTime? UltimoHeartbeatUtc { get; set; }
}
