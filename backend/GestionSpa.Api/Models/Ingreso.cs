namespace GestionSpa.Api.Models;

public class Ingreso : IEmisorEntity
{
    public int Id { get; set; }
    public int EmisorId { get; set; }
    public Emisor Emisor { get; set; } = null!;
    public int SocioId { get; set; }
    public Socio Socio { get; set; } = null!;
    public DateTime FechaHora { get; set; } = DateTime.UtcNow;
    public TipoIngreso Tipo { get; set; } = TipoIngreso.Entrada;
    public bool AccesoPermitido { get; set; } = true;
    public string? MotivoRechazo { get; set; }
}
