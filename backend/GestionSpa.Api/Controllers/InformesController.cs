using GestionSpa.Api.Data;
using GestionSpa.Api.DTOs;
using GestionSpa.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionSpa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InformesController(AppDbContext db) : ControllerBase
{
    [HttpGet("resumen")]
    public async Task<ActionResult<InformeResumenDto>> GetResumen([FromQuery] int? mes, [FromQuery] int? anio)
    {
        var ahora = DateTime.UtcNow;
        var m = mes ?? ahora.Month;
        var a = anio ?? ahora.Year;

        var inicioMes = new DateTime(a, m, 1, 0, 0, 0, DateTimeKind.Utc);
        var finMes = inicioMes.AddMonths(1);

        var pagosMes = await db.Pagos
            .Where(p => p.Fecha >= inicioMes && p.Fecha < finMes)
            .SumAsync(p => p.Monto);

        var cuotasPendientes = await db.CuotasMensuales
            .Where(c => c.Mes == m && c.Anio == a && c.EstadoPago != EstadoPago.Pagado)
            .ToListAsync();

        var totalPendiente = cuotasPendientes.Sum(c => c.Total - c.MontoPagado);

        var cargosPendientes = await db.Cargos
            .Where(c => c.ClienteId != null && c.EstadoPago != EstadoPago.Pagado)
            .CountAsync();

        var totalCargosPendientes = await db.Cargos
            .Where(c => c.ClienteId != null && c.EstadoPago != EstadoPago.Pagado)
            .SumAsync(c => c.Monto * c.Cantidad);

        var hoy = DateTime.UtcNow.Date;
        var ingresosHoy = await db.Ingresos
            .CountAsync(i => i.FechaHora >= hoy && i.Tipo == TipoIngreso.Entrada && i.AccesoPermitido);

        var sociosActivos = await db.Socios.CountAsync(s => s.Estado == EstadoSocio.Activo);

        return new InformeResumenDto(
            pagosMes, totalPendiente + totalCargosPendientes, pagosMes,
            sociosActivos, ingresosHoy, cuotasPendientes.Count, cargosPendientes);
    }

    [HttpGet("cobranza")]
    public async Task<ActionResult<List<InformeCobranzaDto>>> GetCobranza([FromQuery] int? mes, [FromQuery] int? anio)
    {
        var ahora = DateTime.UtcNow;
        var m = mes ?? ahora.Month;
        var a = anio ?? ahora.Year;

        var socios = await db.Socios
            .Where(s => s.Estado != EstadoSocio.Inactivo)
            .OrderBy(s => s.Apellido)
            .ToListAsync();

        var cuotas = await db.CuotasMensuales
            .Where(c => c.Mes == m && c.Anio == a)
            .ToDictionaryAsync(c => c.SocioId);

        var cargosPendientes = await db.Cargos
            .Include(c => c.Servicio)
            .Where(c => c.SocioId != null && !c.SumarACuota && c.EstadoPago != EstadoPago.Pagado)
            .ToListAsync();

        var resultado = socios.Select(s =>
        {
            cuotas.TryGetValue(s.Id, out var cuota);
            var cargosSocio = cargosPendientes.Where(c => c.SocioId == s.Id).ToList();
            var saldoCuota = cuota != null ? cuota.Total - cuota.MontoPagado : 0;
            var saldoCargos = cargosSocio.Sum(c => c.Monto * c.Cantidad);

            return new InformeCobranzaDto(
                s.Id, s.NumeroSocio, $"{s.Nombre} {s.Apellido}",
                saldoCuota + saldoCargos,
                cuota?.MontoPagado ?? 0,
                cuota?.EstadoPago ?? EstadoPago.Pendiente,
                cargosSocio.Select(c => new CargoPendienteDto(
                    c.Id, c.Servicio.Nombre, c.Monto * c.Cantidad, c.Fecha, c.EstadoPago)).ToList());
        }).Where(r => r.TotalPendiente > 0 || r.EstadoCuotaMes != EstadoPago.Pagado)
          .OrderByDescending(r => r.TotalPendiente)
          .ToList();

        return resultado;
    }

    [HttpGet("ingresos-diarios")]
    public async Task<ActionResult<InformeIngresosDto>> GetIngresosDiarios([FromQuery] DateTime? fecha)
    {
        var dia = (fecha ?? DateTime.UtcNow).Date;
        var fin = dia.AddDays(1);

        var ingresos = await db.Ingresos
            .Include(i => i.Socio)
            .Where(i => i.FechaHora >= dia && i.FechaHora < fin)
            .OrderByDescending(i => i.FechaHora)
            .ToListAsync();

        return new InformeIngresosDto(
            dia,
            ingresos.Count(i => i.Tipo == TipoIngreso.Entrada),
            ingresos.Count(i => i.AccesoPermitido),
            ingresos.Count(i => !i.AccesoPermitido),
            ingresos.Select(i => new IngresoDto(
                i.Id, i.SocioId, i.Socio?.NumeroSocio ?? "",
                i.Socio != null ? $"{i.Socio.Nombre} {i.Socio.Apellido}" : "",
                i.FechaHora, i.Tipo, i.AccesoPermitido, i.MotivoRechazo)).ToList());
    }

    [HttpGet("pagos")]
    public async Task<ActionResult<List<PagoDto>>> GetPagos(
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] MetodoPago? metodo)
    {
        var query = db.Pagos.AsQueryable();

        if (desde.HasValue) query = query.Where(p => p.Fecha >= desde);
        if (hasta.HasValue) query = query.Where(p => p.Fecha <= hasta);
        if (metodo.HasValue) query = query.Where(p => p.MetodoPago == metodo);

        var pagos = await query.OrderByDescending(p => p.Fecha).Take(500).ToListAsync();
        return pagos.Select(p => new PagoDto(
            p.Id, p.Monto, p.MetodoPago, p.Fecha,
            p.Referencia, p.RegistradoPor, p.CargoId, p.CuotaMensualId)).ToList();
    }

    [HttpGet("servicios-mas-vendidos")]
    public async Task<ActionResult> GetServiciosMasVendidos([FromQuery] int? mes, [FromQuery] int? anio)
    {
        var ahora = DateTime.UtcNow;
        var m = mes ?? ahora.Month;
        var a = anio ?? ahora.Year;
        var inicio = new DateTime(a, m, 1, 0, 0, 0, DateTimeKind.Utc);
        var fin = inicio.AddMonths(1);

        var datos = await db.Cargos
            .Include(c => c.Servicio)
            .Where(c => c.Fecha >= inicio && c.Fecha < fin)
            .GroupBy(c => new { c.ServicioId, c.Servicio.Nombre })
            .Select(g => new
            {
                g.Key.ServicioId,
                g.Key.Nombre,
                Cantidad = g.Sum(c => c.Cantidad),
                Total = g.Sum(c => c.Monto * c.Cantidad)
            })
            .OrderByDescending(x => x.Cantidad)
            .Take(10)
            .ToListAsync();

        return Ok(datos);
    }
}
