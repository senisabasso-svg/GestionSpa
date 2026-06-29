using GestionSpa.Api.Data;
using GestionSpa.Api.DTOs;
using GestionSpa.Api.Models;
using GestionSpa.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionSpa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CuotasController(AppDbContext db, CuotaService cuotaService, ITenantContext tenant) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CuotaMensualDto>>> GetAll(
        [FromQuery] int? mes, [FromQuery] int? anio, [FromQuery] EstadoPago? estado)
    {
        var query = db.CuotasMensuales.ForTenant(tenant)
            .Include(c => c.Socio)
            .Where(c => c.Socio.Estado == EstadoSocio.Activo)
            .AsQueryable();

        if (mes.HasValue) query = query.Where(c => c.Mes == mes);
        if (anio.HasValue) query = query.Where(c => c.Anio == anio);
        if (estado.HasValue) query = query.Where(c => c.EstadoPago == estado);

        var cuotas = await query.OrderByDescending(c => c.Anio).ThenByDescending(c => c.Mes).ToListAsync();
        return cuotas.Select(Map).ToList();
    }

    [HttpGet("socio/{socioId}")]
    public async Task<ActionResult<List<CuotaMensualDto>>> GetBySocio(int socioId)
    {
        var cuotas = await db.CuotasMensuales.ForTenant(tenant)
            .Include(c => c.Socio)
            .Where(c => c.SocioId == socioId)
            .OrderByDescending(c => c.Anio).ThenByDescending(c => c.Mes)
            .ToListAsync();

        return cuotas.Select(Map).ToList();
    }

    [HttpPost("{id}/pagar")]
    public async Task<ActionResult<PagoDto>> PagarCuota(int id, RegistrarPagoDto dto)
    {
        var emisorId = tenant.RequireEmisorId();
        var cuota = await db.CuotasMensuales.ForTenant(tenant).FirstOrDefaultAsync(c => c.Id == id);
        if (cuota == null) return NotFound();

        if (cuota.EstadoPago == EstadoPago.Pagado)
            return BadRequest(new { mensaje = "La cuota ya está pagada" });

        var pagoErrors = ValidationHelper.ValidateMontoPago(dto.Monto);
        if (pagoErrors.Count > 0) return ValidationHelper.ToBadRequest(pagoErrors);

        var saldoPendiente = cuota.Total - cuota.MontoPagado;
        if (dto.Monto > saldoPendiente)
            return BadRequest(new { mensaje = $"El monto supera el saldo pendiente ({saldoPendiente:N0} UYU)" });

        var pago = new Pago
        {
            EmisorId = emisorId,
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
            await cuotaService.MarcarCargosCuotaComoPagadosAsync(id);
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
        var (mesActual, anioActual) = UruguayTime.MesAnioActual();
        var m = mes ?? mesActual;
        var a = anio ?? anioActual;

        var sociosActivos = await db.Socios.ForTenant(tenant).Where(s => s.Estado == EstadoSocio.Activo).ToListAsync();
        var generadas = 0;

        foreach (var socio in sociosActivos)
        {
            var existe = await db.CuotasMensuales.ForTenant(tenant)
                .AnyAsync(c => c.SocioId == socio.Id && c.Mes == m && c.Anio == a);
            if (!existe)
            {
                await cuotaService.ObtenerOCrearCuotaAsync(socio.Id, m, a);
                generadas++;
            }
        }

        var mensaje = generadas > 0
            ? $"Se generaron {generadas} cuotas para {m}/{a}"
            : $"Las cuotas para {m}/{a} ya fueron generadas";
        return Ok(new { mensaje, generadas });
    }

    private static CuotaMensualDto Map(CuotaMensual c) => new(
        c.Id, c.SocioId, c.Socio.NumeroSocio, $"{c.Socio.Nombre} {c.Socio.Apellido}",
        c.Mes, c.Anio, c.MontoCuota, c.MontoServicios,
        c.Total, c.MontoPagado, c.Total - c.MontoPagado,
        c.EstadoPago, c.FechaVencimiento, c.FechaPago);
}
