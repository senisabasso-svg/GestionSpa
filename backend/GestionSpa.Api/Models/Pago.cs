namespace GestionSpa.Api.Models;

public class Pago
{
    public int Id { get; set; }
    public int? CargoId { get; set; }
    public Cargo? Cargo { get; set; }
    public int? CuotaMensualId { get; set; }
    public CuotaMensual? CuotaMensual { get; set; }
    public decimal Monto { get; set; }
    public MetodoPago MetodoPago { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string? Referencia { get; set; }
    public string? RegistradoPor { get; set; }
    public string? Notas { get; set; }
}
