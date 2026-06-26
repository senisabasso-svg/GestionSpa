namespace GestionSpa.Api.Models;

public class CuotaMensual
{
    public int Id { get; set; }
    public int SocioId { get; set; }
    public Socio Socio { get; set; } = null!;
    public int Mes { get; set; }
    public int Anio { get; set; }
    public decimal MontoCuota { get; set; }
    public decimal MontoServicios { get; set; }
    public decimal Total => MontoCuota + MontoServicios;
    public decimal MontoPagado { get; set; }
    public EstadoPago EstadoPago { get; set; } = EstadoPago.Pendiente;
    public DateTime? FechaVencimiento { get; set; }
    public DateTime? FechaPago { get; set; }

    public ICollection<Cargo> Cargos { get; set; } = [];
    public ICollection<Pago> Pagos { get; set; } = [];
}
