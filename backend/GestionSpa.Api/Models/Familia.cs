namespace GestionSpa.Api.Models;

public class Familia : IEmisorEntity
{
    public int Id { get; set; }
    public int EmisorId { get; set; }
    public Emisor Emisor { get; set; } = null!;
    public string Nombre { get; set; } = string.Empty;
    public decimal CuotaMensual { get; set; }
    public string? Observaciones { get; set; }

    public ICollection<Socio> Socios { get; set; } = [];
}
