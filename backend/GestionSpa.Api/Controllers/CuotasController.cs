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
        [FromQuery] int? mes, [FromQuery] int? anio, [FromQuery] EstadoPago? estado, [FromQuery] string? buscar)
    {
        var emisorId = tenant.RequireEmisorId();

        // Con mes/año: consolidar cobro familiar (titular = monto familia; integrantes = 0).
        if (mes.HasValue && anio.HasValue)
            await cuotaService.NormalizarCuotasFamiliasDelMesAsync(emisorId, mes.Value, anio.Value);

        var query = db.CuotasMensuales.ForTenant(tenant)
            .Include(c => c.Socio).ThenInclude(s => s.Familia)
            .Where(c => c.Socio.Estado == EstadoSocio.Activo)
            .AsQueryable();

        if (mes.HasValue) query = query.Where(c => c.Mes == mes);
        if (anio.HasValue) query = query.Where(c => c.Anio == anio);

        var cuotas = await query.OrderByDescending(c => c.Anio).ThenByDescending(c => c.Mes).ToListAsync();

        var items = ConstruirListado(cuotas);

        if (estado.HasValue)
            items = items.Where(c => c.EstadoPago == estado).ToList();

        var term = ValidationHelper.SanitizeSearchTerm(buscar);
        if (term != null)
        {
            items = items.Where(c =>
            {
                if (ValidationHelper.MatchesSearch(term, c.NumeroSocio, c.SocioNombre))
                    return true;
                if (c.Integrantes == null) return false;
                return c.Integrantes.Any(i => ValidationHelper.MatchesSearch(
                    term, i.NumeroSocio, i.NombreCompleto));
            }).ToList();
        }

        return items;
    }

    [HttpGet("socio/{socioId}")]
    public async Task<ActionResult<List<CuotaMensualDto>>> GetBySocio(int socioId)
    {
        var cuotas = await db.CuotasMensuales.ForTenant(tenant)
            .Include(c => c.Socio).ThenInclude(s => s.Familia)
            .Where(c => c.SocioId == socioId)
            .OrderByDescending(c => c.Anio).ThenByDescending(c => c.Mes)
            .ToListAsync();

        return cuotas.Select(c => MapIndividual(c)).ToList();
    }

    /// <summary>
    /// Deshace el último cobro de la cuota (o del lote familiar) y recalcula a Pendiente/Parcial.
    /// </summary>
    [HttpPost("{id}/revertir-ultimo-pago")]
    public async Task<ActionResult<object>> RevertirUltimoPago(int id)
    {
        var cuota = await db.CuotasMensuales.ForTenant(tenant)
            .Include(c => c.Socio)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (cuota == null) return NotFound();

        List<Pago> aRevertir;

        if (cuota.Socio.FamiliaId.HasValue)
        {
            var familiaId = cuota.Socio.FamiliaId.Value;
            var pagosFamilia = await db.Pagos.ForTenant(tenant)
                .Include(p => p.CuotaMensual!).ThenInclude(c => c!.Socio)
                .Where(p => p.CuotaMensualId != null
                            && p.CuotaMensual!.Mes == cuota.Mes
                            && p.CuotaMensual.Anio == cuota.Anio
                            && p.CuotaMensual.Socio.FamiliaId == familiaId)
                .OrderByDescending(p => p.Fecha)
                .ThenByDescending(p => p.Id)
                .ToListAsync();

            if (pagosFamilia.Count == 0)
                return BadRequest(new { mensaje = "No hay pagos para revertir en esta familia" });

            var ultimo = pagosFamilia[0].Fecha.ToUniversalTime();
            // Mismo lote de cobro familiar (unos segundos).
            aRevertir = pagosFamilia
                .Where(p => Math.Abs((p.Fecha.ToUniversalTime() - ultimo).TotalSeconds) <= 5)
                .ToList();
        }
        else
        {
            var ultimoPago = await db.Pagos.ForTenant(tenant)
                .Where(p => p.CuotaMensualId == id)
                .OrderByDescending(p => p.Fecha)
                .ThenByDescending(p => p.Id)
                .FirstOrDefaultAsync();

            if (ultimoPago == null)
                return BadRequest(new { mensaje = "No hay pagos para revertir en esta cuota" });

            aRevertir = [ultimoPago];
        }

        var cuotasAfectadas = new HashSet<int>();
        int? familiaIdNorm = null;
        foreach (var pago in aRevertir)
        {
            if (pago.CuotaMensualId is not int cuotaId) continue;
            var c = await db.CuotasMensuales.ForTenant(tenant)
                .Include(x => x.Socio)
                .FirstOrDefaultAsync(x => x.Id == cuotaId);
            if (c == null) continue;

            c.MontoPagado = Math.Max(0, c.MontoPagado - pago.Monto);
            CuotaService.RecalcularEstadoPago(c);
            if (c.EstadoPago != EstadoPago.Pagado)
                c.FechaPago = null;
            cuotasAfectadas.Add(c.Id);
            familiaIdNorm = c.Socio.FamiliaId ?? familiaIdNorm;
            db.Pagos.Remove(pago);
        }

        await db.SaveChangesAsync();

        foreach (var cuotaId in cuotasAfectadas)
            await cuotaService.SincronizarCargosConEstadoCuotaAsync(cuotaId);

        if (familiaIdNorm.HasValue)
        {
            var cuotasFam = await db.CuotasMensuales.ForTenant(tenant)
                .Include(c => c.Socio)
                .Where(c => c.Mes == cuota.Mes && c.Anio == cuota.Anio
                            && c.Socio.FamiliaId == familiaIdNorm
                            && c.Socio.Estado == EstadoSocio.Activo)
                .ToListAsync();
            foreach (var c in cuotasFam)
            {
                if (c.MontoPagado > 0) continue;
                if (c.Total <= 0 && c.EstadoPago == EstadoPago.Pagado)
                {
                    c.EstadoPago = EstadoPago.Pendiente;
                    c.FechaPago = null;
                }
            }
            await db.SaveChangesAsync();
            await cuotaService.NormalizarCuotasFamiliaAsync(familiaIdNorm.Value, cuota.Mes, cuota.Anio);
        }

        return Ok(new
        {
            mensaje = aRevertir.Count == 1
                ? "Último pago revertido. La cuota quedó pendiente o parcial."
                : $"Se revirtieron {aRevertir.Count} pagos del último cobro.",
            revertidos = aRevertir.Count
        });
    }

    [HttpPost("{id}/pagar")]
    public async Task<ActionResult<PagoRegistradoDto>> PagarCuota(int id, RegistrarPagoDto dto)
    {
        var emisorId = tenant.RequireEmisorId();
        var cuota = await db.CuotasMensuales.ForTenant(tenant)
            .Include(c => c.Socio).ThenInclude(s => s.Familia)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (cuota == null) return NotFound();

        if (cuota.EstadoPago == EstadoPago.Pagado)
            return BadRequest(new { mensaje = "La cuota ya está pagada" });

        var pagoErrors = ValidationHelper.ValidateMontoPago(dto.Monto);
        if (pagoErrors.Count > 0) return ValidationHelper.ToBadRequest(pagoErrors);

        if (cuota.Socio.FamiliaId.HasValue)
            return await PagarFamiliaAsync(emisorId, cuota, dto);

        var saldoPendiente = cuota.Total - cuota.MontoPagado;
        if (dto.Monto > saldoPendiente)
            return BadRequest(new { mensaje = $"El monto supera el saldo pendiente ({saldoPendiente:N0} UYU)" });

        var pago = RegistrarPagoEnCuota(emisorId, cuota, dto);
        await ActualizarEstadoTrasPagoAsync(cuota);
        await db.SaveChangesAsync();

        return ToPagoRegistrado(pago, [pago.Id]);
    }

    [HttpPost("generar")]
    public async Task<ActionResult> GenerarCuotasMes([FromQuery] int? mes, [FromQuery] int? anio)
    {
        var emisorId = tenant.RequireEmisorId();
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

        await cuotaService.NormalizarCuotasFamiliasDelMesAsync(emisorId, m, a);

        var mensaje = generadas > 0
            ? $"Se generaron {generadas} cuotas para {m}/{a}"
            : $"Las cuotas para {m}/{a} ya fueron generadas";
        return Ok(new { mensaje, generadas });
    }

    private async Task<ActionResult<PagoRegistradoDto>> PagarFamiliaAsync(int emisorId, CuotaMensual cuotaOrigen, RegistrarPagoDto dto)
    {
        var familiaId = cuotaOrigen.Socio.FamiliaId!.Value;
        await cuotaService.NormalizarCuotasFamiliaAsync(familiaId, cuotaOrigen.Mes, cuotaOrigen.Anio);

        var cuotasFamilia = await db.CuotasMensuales.ForTenant(tenant)
            .Include(c => c.Socio)
            .Where(c => c.Mes == cuotaOrigen.Mes && c.Anio == cuotaOrigen.Anio
                        && c.Socio.FamiliaId == familiaId
                        && c.Socio.Estado == EstadoSocio.Activo)
            .OrderBy(c => c.SocioId)
            .ToListAsync();

        if (cuotasFamilia.Count == 0)
            return BadRequest(new { mensaje = "No hay cuotas de la familia para este período" });

        var saldoFamilia = cuotasFamilia.Sum(c => c.Total - c.MontoPagado);
        if (saldoFamilia <= 0)
            return BadRequest(new { mensaje = "La cuota familiar ya está pagada" });
        if (dto.Monto > saldoFamilia)
            return BadRequest(new { mensaje = $"El monto supera el saldo familiar pendiente ({saldoFamilia:N0} UYU)" });

        var remaining = dto.Monto;
        var creados = new List<Pago>();

        foreach (var cuota in cuotasFamilia)
        {
            if (remaining <= 0) break;
            var need = cuota.Total - cuota.MontoPagado;
            if (need <= 0) continue;

            var apply = Math.Min(remaining, need);
            var pagoParcial = new RegistrarPagoDto(apply, dto.MetodoPago, dto.Referencia, dto.RegistradoPor, dto.Notas, null, cuota.Id);
            var pago = RegistrarPagoEnCuota(emisorId, cuota, pagoParcial);
            creados.Add(pago);
            await ActualizarEstadoTrasPagoAsync(cuota);
            remaining -= apply;
        }

        await db.SaveChangesAsync();
        await cuotaService.MarcarIntegrantesFamiliaCubiertosAsync(familiaId, cuotaOrigen.Mes, cuotaOrigen.Anio);

        if (creados.Count == 0)
            return BadRequest(new { mensaje = "No se pudo registrar el pago" });

        return ToPagoRegistrado(creados[0], creados.Select(p => p.Id).ToList());
    }

    private Pago RegistrarPagoEnCuota(int emisorId, CuotaMensual cuota, RegistrarPagoDto dto)
    {
        var pago = new Pago
        {
            EmisorId = emisorId,
            CuotaMensualId = cuota.Id,
            Monto = dto.Monto,
            MetodoPago = dto.MetodoPago,
            Referencia = dto.Referencia,
            RegistradoPor = dto.RegistradoPor,
            Notas = dto.Notas
        };
        db.Pagos.Add(pago);
        cuota.MontoPagado += dto.Monto;
        return pago;
    }

    private async Task ActualizarEstadoTrasPagoAsync(CuotaMensual cuota)
    {
        var total = cuota.Total;
        if (total > 0 && cuota.MontoPagado >= total)
        {
            cuota.EstadoPago = EstadoPago.Pagado;
            cuota.FechaPago = DateTime.UtcNow;
            await cuotaService.MarcarCargosCuotaComoPagadosAsync(cuota.Id);
        }
        else if (cuota.MontoPagado > 0)
            cuota.EstadoPago = EstadoPago.Parcial;
        else
            cuota.EstadoPago = EstadoPago.Pendiente;
    }

    private static List<CuotaMensualDto> ConstruirListado(List<CuotaMensual> cuotas)
    {
        var result = new List<CuotaMensualDto>();

        foreach (var c in cuotas.Where(x => x.Socio.FamiliaId == null))
            result.Add(MapIndividual(c));

        foreach (var grupo in cuotas.Where(x => x.Socio.FamiliaId != null).GroupBy(x => x.Socio.FamiliaId!.Value))
        {
            var members = grupo.OrderBy(c => c.SocioId).ToList();
            var titular = members[0];
            var familia = titular.Socio.Familia;
            var nombreFamilia = familia?.Nombre ?? "Familia";
            var montoCuota = members.Sum(m => m.MontoCuota);
            var montoServicios = members.Sum(m => m.MontoServicios);
            var montoPagado = members.Sum(m => m.MontoPagado);
            var total = members.Sum(m => m.Total);
            var saldo = total - montoPagado;
            var estado = ResolverEstadoFamilia(members, total, montoPagado);

            var integrantes = members.Select(m => new CuotaFamiliaIntegranteDto(
                m.SocioId,
                m.Socio.NumeroSocio,
                $"{m.Socio.Nombre} {m.Socio.Apellido}".Trim(),
                m.MontoServicios)).ToList();

            result.Add(new CuotaMensualDto(
                titular.Id,
                titular.SocioId,
                "FAM",
                nombreFamilia,
                titular.Mes,
                titular.Anio,
                montoCuota,
                montoServicios,
                total,
                montoPagado,
                saldo,
                estado,
                titular.FechaVencimiento,
                members.Max(m => m.FechaPago),
                EsFamilia: true,
                FamiliaId: grupo.Key,
                Integrantes: integrantes));
        }

        return result
            .OrderByDescending(c => c.Anio)
            .ThenByDescending(c => c.Mes)
            .ThenBy(c => c.SocioNombre)
            .ToList();
    }

    private static EstadoPago ResolverEstadoFamilia(List<CuotaMensual> members, decimal total, decimal montoPagado)
    {
        if (total > 0 && montoPagado >= total) return EstadoPago.Pagado;
        if (members.All(m => m.EstadoPago == EstadoPago.Pagado)) return EstadoPago.Pagado;
        if (montoPagado > 0) return EstadoPago.Parcial;
        return EstadoPago.Pendiente;
    }

    private static CuotaMensualDto MapIndividual(CuotaMensual c) => new(
        c.Id, c.SocioId, c.Socio.NumeroSocio, $"{c.Socio.Nombre} {c.Socio.Apellido}",
        c.Mes, c.Anio, c.MontoCuota, c.MontoServicios,
        c.Total, c.MontoPagado, c.Total - c.MontoPagado,
        c.EstadoPago, c.FechaVencimiento, c.FechaPago);

    private static PagoRegistradoDto ToPagoRegistrado(Pago pago, IReadOnlyList<int> idsRevertibles) => new(
        pago.Id, pago.Monto, pago.MetodoPago, pago.Fecha,
        pago.Referencia, pago.RegistradoPor, pago.CargoId, pago.CuotaMensualId,
        idsRevertibles, SegundosParaRevertir: 10);
}
