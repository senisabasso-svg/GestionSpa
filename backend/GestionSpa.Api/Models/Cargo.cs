namespace GestionSpa.Api.Models;

public class Cargo
{
    public int Id { get; set; }
    public int ServicioId { get; set; }
    public Servicio Servicio { get; set; } = null!;
    public int? SocioId { get; set; }
    public Socio? Socio { get; set; }
    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public int? CuotaMensualId { get; set; }
    public CuotaMensual? CuotaMensual { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public decimal Monto { get; set; }
    public int Cantidad { get; set; } = 1;
    public EstadoPago EstadoPago { get; set; } = EstadoPago.Pendiente;
    public bool SumarACuota { get; set; } = true;
    public string? Notas { get; set; }
    public string? AtendidoPor { get; set; }

    public ICollection<Pago> Pagos { get; set; } = [];
}
