using GestionSpa.Api.Data;
using GestionSpa.Api.DTOs;
using GestionSpa.Api.Models;
using GestionSpa.Api.Services;
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
        var (mesActual, anioActual) = UruguayTime.MesAnioActual();
        var m = mes ?? mesActual;
        var a = anio ?? anioActual;

        var inicioMes = UruguayTime.InicioMesUtc(m, a);
        var finMes = UruguayTime.FinMesUtc(m, a);

        var pagosMes = await db.Pagos
            .Where(p => p.Fecha >= inicioMes && p.Fecha < finMes && p.Monto > 0)
            .SumAsync(p => p.Monto);

        var totalCobradoCuotas = await db.Pagos
            .Where(p => p.Fecha >= inicioMes && p.Fecha < finMes && p.Monto > 0 && p.CuotaMensualId != null)
            .SumAsync(p => p.Monto);

        var cuotasPendientes = await db.CuotasMensuales
            .Where(c => c.Mes == m && c.Anio == a && c.EstadoPago != EstadoPago.Pagado)
            .ToListAsync();

        var totalPendiente = cuotasPendientes.Sum(c => c.Total - c.MontoPagado);

        var cargosPendientes = await db.Cargos
            .Where(c => c.ClienteId != null && c.EstadoPago != EstadoPago.Pagado && c.EstadoPago != EstadoPago.Anulado)
            .CountAsync();

        var totalCargosPendientes = await db.Cargos
            .Where(c => c.ClienteId != null && c.EstadoPago != EstadoPago.Pagado && c.EstadoPago != EstadoPago.Anulado)
            .SumAsync(c => c.Monto * c.Cantidad);

        var inicioHoy = UruguayTime.InicioDiaUtc();
        var finHoy = UruguayTime.FinDiaUtc();
        var ingresosHoy = await db.Ingresos
            .CountAsync(i => i.FechaHora >= inicioHoy && i.FechaHora < finHoy
                && i.Tipo == TipoIngreso.Entrada && i.AccesoPermitido);

        var sociosActivos = await db.Socios.CountAsync(s => s.Estado == EstadoSocio.Activo);

        return new InformeResumenDto(
            pagosMes, totalPendiente + totalCargosPendientes, totalCobradoCuotas,
            sociosActivos, ingresosHoy, cuotasPendientes.Count, cargosPendientes);
    }

    [HttpGet("cobranza")]
    public async Task<ActionResult<List<InformeCobranzaDto>>> GetCobranza([FromQuery] int? mes, [FromQuery] int? anio)
    {
        var (mesActual, anioActual) = UruguayTime.MesAnioActual();
        var m = mes ?? mesActual;
        var a = anio ?? anioActual;

        var socios = await db.Socios
            .Where(s => s.Estado != EstadoSocio.Inactivo)
            .OrderBy(s => s.Apellido)
            .ToListAsync();

        var cuotas = await db.CuotasMensuales
            .Where(c => c.Mes == m && c.Anio == a)
            .ToDictionaryAsync(c => c.SocioId);

        var cargosPendientes = await db.Cargos
            .Include(c => c.Servicio)
            .Where(c => c.SocioId != null && !c.SumarACuota
                && c.EstadoPago != EstadoPago.Pagado && c.EstadoPago != EstadoPago.Anulado)
            .ToListAsync();

        var resultado = socios.Select(s =>
        {
            cuotas.TryGetValue(s.Id, out var cuota);
            var cargosSocio = cargosPendientes.Where(c => c.SocioId == s.Id).ToList();
            var saldoCuota = cuota != null ? cuota.Total - cuota.MontoPagado : 0m;
            var saldoCargos = cargosSocio.Sum(c => c.Monto * c.Cantidad);
            var totalPendiente = saldoCuota + saldoCargos;

            if (cuota == null && cargosSocio.Count == 0)
                return (InformeCobranzaDto?)null;

            return new InformeCobranzaDto(
                s.Id, s.NumeroSocio, $"{s.Nombre} {s.Apellido}",
                totalPendiente,
                cuota?.MontoPagado ?? 0,
                cuota?.EstadoPago,
                cuota == null,
                cargosSocio.Select(c => new CargoPendienteDto(
                    c.Id, c.Servicio.Nombre, c.Monto * c.Cantidad, c.Fecha, c.EstadoPago)).ToList());
        })
        .Where(r => r != null && (r.TotalPendiente > 0 || r.EstadoCuotaMes != EstadoPago.Pagado || r.SinCuotaMes))
        .Select(r => r!)
        .OrderByDescending(r => r.TotalPendiente)
        .ToList();

        return resultado;
    }

    [HttpGet("ingresos-diarios")]
    public async Task<ActionResult<InformeIngresosDto>> GetIngresosDiarios([FromQuery] DateTime? fecha)
    {
        var diaLocal = (fecha ?? UruguayTime.Today).Date;
        var inicio = UruguayTime.InicioDiaUtc(diaLocal);
        var fin = UruguayTime.FinDiaUtc(diaLocal);

        var ingresos = await db.Ingresos
            .Include(i => i.Socio)
            .Where(i => i.FechaHora >= inicio && i.FechaHora < fin)
            .OrderByDescending(i => i.FechaHora)
            .ToListAsync();

        var entradas = ingresos.Where(i => i.Tipo == TipoIngreso.Entrada).ToList();

        return new InformeIngresosDto(
            diaLocal,
            entradas.Count,
            entradas.Count(i => i.AccesoPermitido),
            entradas.Count(i => !i.AccesoPermitido),
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
        var (mesActual, anioActual) = UruguayTime.MesAnioActual();
        var m = mes ?? mesActual;
        var a = anio ?? anioActual;
        var inicio = UruguayTime.InicioMesUtc(m, a);
        var fin = UruguayTime.FinMesUtc(m, a);

        var datos = await db.Cargos
            .Include(c => c.Servicio)
            .Where(c => c.Fecha >= inicio && c.Fecha < fin && c.Cantidad > 0
                && c.EstadoPago != EstadoPago.Anulado)
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
