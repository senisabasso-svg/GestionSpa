using GestionSpa.Api.Data;
using GestionSpa.Api.DTOs;
using GestionSpa.Api.Models;
using GestionSpa.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionSpa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CuotasController(AppDbContext db, CuotaService cuotaService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CuotaMensualDto>>> GetAll(
        [FromQuery] int? mes, [FromQuery] int? anio, [FromQuery] EstadoPago? estado)
    {
        var query = db.CuotasMensuales.Include(c => c.Socio).AsQueryable();

        if (mes.HasValue) query = query.Where(c => c.Mes == mes);
        if (anio.HasValue) query = query.Where(c => c.Anio == anio);
        if (estado.HasValue) query = query.Where(c => c.EstadoPago == estado);

        var cuotas = await query.OrderByDescending(c => c.Anio).ThenByDescending(c => c.Mes).ToListAsync();
        return cuotas.Select(Map).ToList();
    }

    [HttpGet("socio/{socioId}")]
    public async Task<ActionResult<List<CuotaMensualDto>>> GetBySocio(int socioId)
    {
        var cuotas = await db.CuotasMensuales
            .Include(c => c.Socio)
            .Where(c => c.SocioId == socioId)
            .OrderByDescending(c => c.Anio).ThenByDescending(c => c.Mes)
            .ToListAsync();

        return cuotas.Select(Map).ToList();
    }

    [HttpPost("{id}/pagar")]
    public async Task<ActionResult<PagoDto>> PagarCuota(int id, RegistrarPagoDto dto)
    {
        var cuota = await db.CuotasMensuales.FindAsync(id);
        if (cuota == null) return NotFound();

        var pagoErrors = ValidationHelper.ValidateMontoPago(dto.Monto);
        if (pagoErrors.Count > 0) return ValidationHelper.ToBadRequest(pagoErrors);

        var pago = new Pago
        {
            CuotaMensualId = id,
            Monto = dto.Monto,
            MetodoPago = dto.MetodoPago,
            Referencia = dto.Referencia,
            RegistradoPor = dto.RegistradoPor,
            Notas = dto.Notas
        };

        db.Pagos.Add(pago);
        cuota.MontoPagado += dto.Monto;

        var total = cuota.Total;
        if (total > 0 && cuota.MontoPagado >= total)
        {
            cuota.EstadoPago = EstadoPago.Pagado;
            cuota.FechaPago = DateTime.UtcNow;
        }
        else if (cuota.MontoPagado > 0)
            cuota.EstadoPago = EstadoPago.Parcial;
        else
            cuota.EstadoPago = EstadoPago.Pendiente;

        await db.SaveChangesAsync();

        return new PagoDto(pago.Id, pago.Monto, pago.MetodoPago, pago.Fecha,
            pago.Referencia, pago.RegistradoPor, pago.CargoId, pago.CuotaMensualId);
    }

    [HttpPost("generar")]
    public async Task<ActionResult> GenerarCuotasMes([FromQuery] int? mes, [FromQuery] int? anio)
    {
        var ahora = DateTime.UtcNow;
        var m = mes ?? ahora.Month;
        var a = anio ?? ahora.Year;

        var sociosActivos = await db.Socios.Where(s => s.Estado == EstadoSocio.Activo).ToListAsync();
        var generadas = 0;

        foreach (var socio in sociosActivos)
        {
            var existe = await db.CuotasMensuales.AnyAsync(c => c.SocioId == socio.Id && c.Mes == m && c.Anio == a);
            if (!existe)
            {
                await cuotaService.ObtenerOCrearCuotaAsync(socio.Id, m, a);
                generadas++;
            }
        }

        return Ok(new { mensaje = $"Se generaron {generadas} cuotas para {m}/{a}" });
    }

    private static CuotaMensualDto Map(CuotaMensual c) => new(
        c.Id, c.SocioId, c.Socio.NumeroSocio, $"{c.Socio.Nombre} {c.Socio.Apellido}",
        c.Mes, c.Anio, c.MontoCuota, c.MontoServicios,
        c.Total, c.MontoPagado, c.Total - c.MontoPagado,
        c.EstadoPago, c.FechaVencimiento, c.FechaPago);
}
