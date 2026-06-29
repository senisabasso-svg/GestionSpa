namespace GestionSpa.Api.Models;

public class Emisor
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Ciudad { get; set; }
    public string? Departamento { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaAlta { get; set; } = DateTime.UtcNow;

    public ICollection<Usuario> Usuarios { get; set; } = [];
    public ICollection<Familia> Familias { get; set; } = [];
    public ICollection<Socio> Socios { get; set; } = [];
    public ICollection<Cliente> Clientes { get; set; } = [];
    public ICollection<Servicio> Servicios { get; set; } = [];
}
