namespace GestionSpa.Api.Models;

public class Servicio : IEmisorEntity
{
    public int Id { get; set; }
    public int EmisorId { get; set; }
    public Emisor Emisor { get; set; } = null!;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public CategoriaServicio Categoria { get; set; }
    public decimal Precio { get; set; }
    public int DuracionMinutos { get; set; }
    public bool Activo { get; set; } = true;
    public bool SoloSocios { get; set; } = false;

    public ICollection<Cargo> Cargos { get; set; } = [];
}
