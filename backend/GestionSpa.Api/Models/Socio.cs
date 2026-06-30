namespace GestionSpa.Api.Models;

public class Socio : IEmisorEntity
{
    public int Id { get; set; }
    public int EmisorId { get; set; }
    public Emisor Emisor { get; set; } = null!;
    public string NumeroSocio { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Cedula { get; set; } = string.Empty;
    public TipoIdentificacionSocio TipoIdentificacion { get; set; } = TipoIdentificacionSocio.Cedula;
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }
    public string? Ciudad { get; set; } = "Salto";
    public string? Departamento { get; set; } = "Salto";
    public DateTime FechaAlta { get; set; } = DateTime.UtcNow;
    public DateTime? FechaVencimiento { get; set; }
    public decimal CuotaMensual { get; set; }
    public MetodoPago MedioPago { get; set; } = MetodoPago.Efectivo;
    public EstadoSocio Estado { get; set; } = EstadoSocio.Activo;
    public string? Observaciones { get; set; }

    public int? FamiliaId { get; set; }
    public Familia? Familia { get; set; }

    public ICollection<Cargo> Cargos { get; set; } = [];
    public ICollection<CuotaMensual> Cuotas { get; set; } = [];
    public ICollection<Ingreso> Ingresos { get; set; } = [];
}
