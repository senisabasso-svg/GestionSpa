namespace GestionSpa.Api.Models;

public class Cliente : IEmisorEntity
{
    public int Id { get; set; }
    public int EmisorId { get; set; }
    public Emisor Emisor { get; set; } = null!;
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string? Cedula { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Ciudad { get; set; } = "Salto";
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public string? Observaciones { get; set; }

    public ICollection<Cargo> Cargos { get; set; } = [];
}
