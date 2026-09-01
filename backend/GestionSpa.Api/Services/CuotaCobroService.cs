using GestionSpa.Api.Data;
using GestionSpa.Api.DTOs;
using GestionSpa.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionSpa.Api.Services;

public class CuotaCobroService(AppDbContext db, CuotaService cuotaService)
{
    public async Task<CuotaMensualDto> PrepararCuotaSocioAsync(int emisorId, int socioId)
    {
        var socio = await db.Socios
            .Include(s => s.Familia)
            .FirstOrDefaultAsync(s => s.Id == socioId && s.EmisorId == emisorId)
            ?? throw new KeyNotFoundException("Socio no encontrado");

        if (socio.Estado != EstadoSocio.Activo)
            throw new InvalidOperationException("Solo se puede generar cuota a socios activos");

        var (mes, anio) = UruguayTime.MesAnioActual();
        await cuotaService.ObtenerOCrearCuotaAsync(socioId, mes, anio);

        if (socio.FamiliaId.HasValue)
        {
            await cuotaService.NormalizarCuotasFamiliaAsync(socio.FamiliaId.Value, mes, anio);
            return await MapCuotaFamiliaAsync(socio.FamiliaId.Value, mes, anio)
                ?? throw new InvalidOperationException("No se pudo preparar la cuota familiar");
        }

        var cuota = await db.CuotasMensuales
            .Include(c => c.Socio)
            .FirstAsync(c => c.SocioId == socioId && c.Mes == mes && c.Anio == anio);

        return MapIndividual(cuota);
    }

    public async Task<PagoRegistradoDto> CobrarCuotaAsync(int emisorId, int cuotaId, RegistrarPagoDto dto)
    {
        var cuota = await db.CuotasMensuales
            .Include(c => c.Socio).ThenInclude(s => s.Familia)
            .FirstOrDefaultAsync(c => c.Id == cuotaId && c.EmisorId == emisorId)
            ?? throw new KeyNotFoundException("Cuota no encontrada");

        if (cuota.EstadoPago == EstadoPago.Pagado)
            throw new InvalidOperationException("La cuota ya está pagada");

        var pagoErrors = ValidationHelper.ValidateMontoPago(dto.Monto);
        if (pagoErrors.Count > 0)
            throw new ArgumentException(string.Join("; ", pagoErrors));

        if (cuota.Socio.FamiliaId.HasValue)
            return await PagarFamiliaAsync(emisorId, cuota, dto);

        var saldoPendiente = cuota.Total - cuota.MontoPagado;
        if (dto.Monto > saldoPendiente)
            throw new InvalidOperationException($"El monto supera el saldo pendiente ({saldoPendiente:N0} UYU)");

        var pago = RegistrarPagoEnCuota(emisorId, cuota, dto);
        await ActualizarEstadoTrasPagoAsync(cuota);
        await db.SaveChangesAsync();

        return ToPagoRegistrado(pago, [pago.Id]);
    }

    public async Task<DateTime> ExtenderVencimientoDesdePagoAsync(IEnumerable<int> socioIds)
    {
        var fechaPagoLocal = UruguayTime.Now;
        var nuevaVencimiento = UruguayTime.VencimientoUnMesDesde(fechaPagoLocal);
        var ids = socioIds.Distinct().ToList();

        var socios = await db.Socios.Where(s => ids.Contains(s.Id)).ToListAsync();
        foreach (var socio in socios)
            socio.FechaVencimiento = nuevaVencimiento;

        await db.SaveChangesAsync();
        return nuevaVencimiento;
    }

    public async Task<IReadOnlyList<int>> ObtenerSociosParaExtenderVencimientoAsync(int socioId, CuotaMensualDto cuotaDto)
    {
        if (cuotaDto.EsFamilia && cuotaDto.FamiliaId.HasValue)
        {
            return await db.Socios
                .Where(s => s.FamiliaId == cuotaDto.FamiliaId && s.Estado == EstadoSocio.Activo)
                .Select(s => s.Id)
                .ToListAsync();
        }

        return [socioId];
    }

    private async Task<CuotaMensualDto?> MapCuotaFamiliaAsync(int familiaId, int mes, int anio)
    {
        var members = await db.CuotasMensuales
            .Include(c => c.Socio).ThenInclude(s => s.Familia)
            .Where(c => c.Mes == mes && c.Anio == anio
                        && c.Socio.FamiliaId == familiaId
                        && c.Socio.Estado == EstadoSocio.Activo)
            .OrderBy(c => c.SocioId)
            .ToListAsync();

        if (members.Count == 0) return null;

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

        return new CuotaMensualDto(
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
            FamiliaId: familiaId,
            Integrantes: integrantes);
    }

    private async Task<PagoRegistradoDto> PagarFamiliaAsync(int emisorId, CuotaMensual cuotaOrigen, RegistrarPagoDto dto)
    {
        var familiaId = cuotaOrigen.Socio.FamiliaId!.Value;
        await cuotaService.NormalizarCuotasFamiliaAsync(familiaId, cuotaOrigen.Mes, cuotaOrigen.Anio);

        var cuotasFamilia = await db.CuotasMensuales
            .Include(c => c.Socio)
            .Where(c => c.Mes == cuotaOrigen.Mes && c.Anio == cuotaOrigen.Anio
                        && c.Socio.FamiliaId == familiaId
                        && c.Socio.Estado == EstadoSocio.Activo)
            .OrderBy(c => c.SocioId)
            .ToListAsync();

        if (cuotasFamilia.Count == 0)
            throw new InvalidOperationException("No hay cuotas de la familia para este período");

        var saldoFamilia = cuotasFamilia.Sum(c => c.Total - c.MontoPagado);
        if (saldoFamilia <= 0)
            throw new InvalidOperationException("La cuota familiar ya está pagada");
        if (dto.Monto > saldoFamilia)
            throw new InvalidOperationException($"El monto supera el saldo familiar pendiente ({saldoFamilia:N0} UYU)");

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
            throw new InvalidOperationException("No se pudo registrar el pago");

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

    private static EstadoPago ResolverEstadoFamilia(List<CuotaMensual> members, decimal total, decimal montoPagado)
    {
        if (total > 0 && montoPagado >= total) return EstadoPago.Pagado;
        if (members.All(m => m.EstadoPago == EstadoPago.Pagado)) return EstadoPago.Pagado;
        if (montoPagado > 0) return EstadoPago.Parcial;
        return EstadoPago.Pendiente;
    }

    private static CuotaMensualDto MapIndividual(CuotaMensual c) => new(
        c.Id, c.SocioId, c.Socio.NumeroSocio, $"{c.Socio.Nombre} {c.Socio.Apellido}".Trim(),
        c.Mes, c.Anio, c.MontoCuota, c.MontoServicios,
        c.Total, c.MontoPagado, c.Total - c.MontoPagado,
        c.EstadoPago, c.FechaVencimiento, c.FechaPago);

    private static PagoRegistradoDto ToPagoRegistrado(Pago pago, IReadOnlyList<int> idsRevertibles) => new(
        pago.Id, pago.Monto, pago.MetodoPago, pago.Fecha,
        pago.Referencia, pago.RegistradoPor, pago.CargoId, pago.CuotaMensualId,
        idsRevertibles, SegundosParaRevertir: 10);
}
