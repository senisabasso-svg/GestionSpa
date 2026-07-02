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
public class InformesController(AppDbContext db, ITenantContext tenant) : ControllerBase
{
    [HttpGet("resumen")]
    public async Task<ActionResult<InformeResumenDto>> GetResumen([FromQuery] int? mes, [FromQuery] int? anio)
    {
        var (mesActual, anioActual) = UruguayTime.MesAnioActual();
        var m = mes ?? mesActual;
        var a = anio ?? anioActual;

        var inicioMes = UruguayTime.InicioMesUtc(m, a);
        var finMes = UruguayTime.FinMesUtc(m, a);

        var pagosMes = await db.Pagos.ForTenant(tenant)
            .Where(p => p.Fecha >= inicioMes && p.Fecha < finMes && p.Monto > 0)
            .SumAsync(p => p.Monto);

        var totalCobradoCuotas = await db.Pagos.ForTenant(tenant)
            .Where(p => p.Fecha >= inicioMes && p.Fecha < finMes && p.Monto > 0 && p.CuotaMensualId != null)
            .SumAsync(p => p.Monto);

        var cuotasPendientes = await db.CuotasMensuales.ForTenant(tenant)
            .Include(c => c.Socio)
            .Where(c => c.Mes == m && c.Anio == a && c.EstadoPago != EstadoPago.Pagado
                && c.Socio.Estado == EstadoSocio.Activo)
            .ToListAsync();

        var totalPendiente = cuotasPendientes.Sum(c => c.Total - c.MontoPagado);

        var cargosPendientes = await db.Cargos.ForTenant(tenant)
            .Where(c => c.ClienteId != null && c.EstadoPago != EstadoPago.Pagado && c.EstadoPago != EstadoPago.Anulado)
            .CountAsync();

        var totalCargosPendientes = await db.Cargos.ForTenant(tenant)
            .Where(c => c.ClienteId != null && c.EstadoPago != EstadoPago.Pagado && c.EstadoPago != EstadoPago.Anulado)
            .SumAsync(c => c.Monto * c.Cantidad);

        var inicioHoy = UruguayTime.InicioDiaUtc();
        var finHoy = UruguayTime.FinDiaUtc();
        var ingresosHoy = await db.Ingresos.ForTenant(tenant)
            .CountAsync(i => i.FechaHora >= inicioHoy && i.FechaHora < finHoy
                && i.Tipo == TipoIngreso.Entrada && i.AccesoPermitido);

        var sociosActivos = await db.Socios.ForTenant(tenant).CountAsync(s => s.Estado == EstadoSocio.Activo);

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

        var socios = await db.Socios.ForTenant(tenant)
            .Where(s => s.Estado != EstadoSocio.Inactivo)
            .OrderBy(s => s.Apellido)
            .ToListAsync();

        var cuotas = await db.CuotasMensuales.ForTenant(tenant)
            .Where(c => c.Mes == m && c.Anio == a)
            .ToDictionaryAsync(c => c.SocioId);

        var cargosPendientes = await db.Cargos.ForTenant(tenant)
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

        var ingresos = await db.Ingresos.ForTenant(tenant)
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
        var query = db.Pagos.ForTenant(tenant).AsQueryable();

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

        var datos = await db.Cargos.ForTenant(tenant)
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

    [HttpGet("socios-activos")]
    public async Task<ActionResult<InformeSociosActivosDto>> GetSociosActivos([FromQuery] int? mes, [FromQuery] int? anio)
    {
        var (mesActual, anioActual) = UruguayTime.MesAnioActual();
        var m = mes ?? mesActual;
        var a = anio ?? anioActual;
        return await BuildInformeSociosActivosAsync(m, a);
    }

    [HttpGet("socios-activos/export")]
    public async Task<IActionResult> ExportSociosActivos([FromQuery] int? mes, [FromQuery] int? anio)
    {
        var (mesActual, anioActual) = UruguayTime.MesAnioActual();
        var m = mes ?? mesActual;
        var a = anio ?? anioActual;
        var informe = await BuildInformeSociosActivosAsync(m, a);
        return BuildSociosCsvFile(informe.Socios, $"socios-activos-{m:D2}-{a}", includeCuotaMes: true);
    }

    [HttpGet("socios/export")]
    public async Task<IActionResult> ExportSociosPorRango(
        [FromQuery] string tipo,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] int? mes,
        [FromQuery] int? anio)
    {
        if (string.IsNullOrWhiteSpace(tipo) || (tipo != "activos" && tipo != "inactivos"))
            return BadRequest(new { mensaje = "El tipo debe ser 'activos' o 'inactivos'" });

        if (!desde.HasValue || !hasta.HasValue)
            return BadRequest(new { mensaje = "Debés indicar fecha desde y hasta" });

        if (desde.Value.Date > hasta.Value.Date)
            return BadRequest(new { mensaje = "La fecha desde no puede ser posterior a la hasta" });

        var (mesActual, anioActual) = UruguayTime.MesAnioActual();
        var m = mes ?? mesActual;
        var a = anio ?? anioActual;

        var estado = tipo == "activos" ? EstadoSocio.Activo : EstadoSocio.Inactivo;
        var socios = await GetSociosPorEstadoYRangoAsync(estado, desde, hasta);

        Dictionary<int, CuotaMensual> cuotas = new();
        if (tipo == "activos")
        {
            cuotas = await db.CuotasMensuales.ForTenant(tenant)
                .Where(c => c.Mes == m && c.Anio == a)
                .ToDictionaryAsync(c => c.SocioId);
        }

        var items = socios.Select(s =>
        {
            cuotas.TryGetValue(s.Id, out var cuota);
            var sinCuota = cuota == null;
            var saldo = cuota != null ? cuota.Total - cuota.MontoPagado : 0m;
            return new InformeSocioActivoDto(
                s.Id, s.NumeroSocio, s.Nombre, s.Apellido, s.Cedula, s.TipoIdentificacion,
                s.Telefono, s.Email, s.Familia?.Nombre, s.Ciudad, s.CuotaMensual, s.MedioPago,
                s.FechaAlta, s.FechaVencimiento,
                cuota?.EstadoPago, sinCuota, saldo);
        }).ToList();

        var desdeStr = desde.Value.ToString("yyyy-MM-dd");
        var hastaStr = hasta.Value.ToString("yyyy-MM-dd");
        var fileName = $"socios-{tipo}-{desdeStr}_{hastaStr}";
        return BuildSociosCsvFile(items, fileName, includeCuotaMes: tipo == "activos");
    }

    [Authorize(Roles = nameof(RolUsuario.SuperAdmin))]
    [HttpPost("sorteo")]
    public async Task<ActionResult<ResultadoSorteoDto>> GenerarSorteo()
    {
        var activos = await db.Socios.ForTenant(tenant)
            .Where(s => s.Estado == EstadoSocio.Activo)
            .OrderBy(s => s.Id)
            .ToListAsync();

        if (activos.Count == 0)
            return BadRequest(new { mensaje = "No hay socios activos para realizar el sorteo" });

        var ganador = activos[Random.Shared.Next(activos.Count)];
        return new ResultadoSorteoDto(
            ganador.Id,
            ganador.NumeroSocio,
            $"{ganador.Nombre} {ganador.Apellido}",
            ganador.Cedula,
            activos.Count,
            DateTime.UtcNow);
    }

    private async Task<InformeSociosActivosDto> BuildInformeSociosActivosAsync(int m, int a)
    {
        var socios = await db.Socios.ForTenant(tenant)
            .Include(s => s.Familia)
            .Where(s => s.Estado == EstadoSocio.Activo)
            .OrderBy(s => s.Apellido).ThenBy(s => s.Nombre)
            .ToListAsync();

        var cuotas = await db.CuotasMensuales.ForTenant(tenant)
            .Where(c => c.Mes == m && c.Anio == a)
            .ToDictionaryAsync(c => c.SocioId);

        var items = socios.Select(s =>
        {
            cuotas.TryGetValue(s.Id, out var cuota);
            var sinCuota = cuota == null;
            var saldo = cuota != null ? cuota.Total - cuota.MontoPagado : 0m;
            return new InformeSocioActivoDto(
                s.Id, s.NumeroSocio, s.Nombre, s.Apellido, s.Cedula, s.TipoIdentificacion,
                s.Telefono, s.Email, s.Familia?.Nombre, s.Ciudad, s.CuotaMensual, s.MedioPago,
                s.FechaAlta, s.FechaVencimiento,
                cuota?.EstadoPago, sinCuota, saldo);
        }).ToList();

        var resumen = new InformeSociosActivosResumenDto(
            items.Count,
            items.Count(i => i.EstadoCuotaMes == EstadoPago.Pagado),
            items.Count(i => i.EstadoCuotaMes is EstadoPago.Pendiente or EstadoPago.Parcial),
            items.Count(i => i.SinCuotaMes),
            items.Count(i => !string.IsNullOrEmpty(i.FamiliaNombre)),
            items.Count(i => string.IsNullOrEmpty(i.FamiliaNombre)),
            items.Sum(i => i.CuotaMensual));

        return new InformeSociosActivosDto(m, a, resumen, items);
    }

    private async Task<List<Socio>> GetSociosPorEstadoYRangoAsync(
        EstadoSocio estado, DateTime? desde, DateTime? hasta)
    {
        var query = db.Socios.ForTenant(tenant).Include(s => s.Familia)
            .Where(s => s.Estado == estado);

        if (desde.HasValue)
        {
            var inicio = UruguayTime.InicioDiaUtc(desde.Value.Date);
            query = query.Where(s => s.FechaAlta >= inicio);
        }
        if (hasta.HasValue)
        {
            var fin = UruguayTime.FinDiaUtc(hasta.Value.Date);
            query = query.Where(s => s.FechaAlta < fin);
        }

        return await query.OrderBy(s => s.Apellido).ThenBy(s => s.Nombre).ToListAsync();
    }

    private IActionResult BuildSociosCsvFile(List<InformeSocioActivoDto> socios, string fileBaseName, bool includeCuotaMes)
    {
        var sb = new System.Text.StringBuilder();
        if (includeCuotaMes)
            sb.AppendLine("Nº Socio;Nombre;Apellido;Localidad;Tipo documento;Documento;Teléfono;Email;Familia;Cuota mensual;Medio de pago;Fecha alta;Vencimiento;Estado cuota mes;Saldo cuota mes");
        else
            sb.AppendLine("Nº Socio;Nombre;Apellido;Localidad;Tipo documento;Documento;Teléfono;Email;Familia;Cuota mensual;Medio de pago;Fecha alta;Vencimiento");

        foreach (var s in socios)
        {
            var tipoDoc = s.TipoIdentificacion == TipoIdentificacionSocio.Cedula ? "Cédula" : "Otro";
            var cells = new List<string>
            {
                CsvCell(s.NumeroSocio), CsvCell(s.Nombre), CsvCell(s.Apellido), CsvCell(s.Localidad),
                CsvCell(tipoDoc),
                CsvCell(s.Cedula), CsvCell(s.Telefono), CsvCell(s.Email), CsvCell(s.FamiliaNombre),
                s.CuotaMensual.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
                CsvCell(s.MedioPago.ToString()), CsvCell(s.FechaAlta.ToString("dd/MM/yyyy")),
                CsvCell(s.FechaVencimiento?.ToString("dd/MM/yyyy")),
            };
            if (includeCuotaMes)
            {
                var estadoCuota = s.SinCuotaMes ? "Sin cuota" : (s.EstadoCuotaMes?.ToString() ?? "—");
                cells.Add(CsvCell(estadoCuota));
                cells.Add(s.SaldoCuotaMes.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            }
            sb.AppendLine(string.Join(';', cells));
        }

        var bytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"{fileBaseName}.csv");
    }

    private static string CsvCell(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var escaped = value.Replace("\"", "\"\"");
        return escaped.Contains(';') || escaped.Contains('"') || escaped.Contains('\n')
            ? $"\"{escaped}\""
            : escaped;
    }
}
