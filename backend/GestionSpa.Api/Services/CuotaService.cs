using GestionSpa.Api.Data;
using GestionSpa.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionSpa.Api.Services;

public class CuotaService(AppDbContext db)
{
    public async Task<CuotaMensual> ObtenerOCrearCuotaAsync(int socioId, int mes, int anio)
    {
        var cuota = await db.CuotasMensuales
            .FirstOrDefaultAsync(c => c.SocioId == socioId && c.Mes == mes && c.Anio == anio);

        if (cuota != null) return cuota;

        var socio = await db.Socios.AsNoTracking()
            .Include(s => s.Familia)
            .FirstOrDefaultAsync(s => s.Id == socioId)
            ?? throw new KeyNotFoundException("Socio no encontrado");

        // Socios en familia: el monto de cuota lo define la familia (titular); acá arranca en 0.
        var montoCuota = socio.FamiliaId.HasValue ? 0m : socio.CuotaMensual;

        cuota = new CuotaMensual
        {
            EmisorId = socio.EmisorId,
            SocioId = socioId,
            Mes = mes,
            Anio = anio,
            MontoCuota = montoCuota,
            FechaVencimiento = UruguayTime.VencimientoCuota(mes, anio)
        };

        db.CuotasMensuales.Add(cuota);

        for (var intento = 0; intento < 3; intento++)
        {
            try
            {
                await db.SaveChangesAsync();
                if (socio.FamiliaId.HasValue)
                    await NormalizarCuotasFamiliaAsync(socio.FamiliaId.Value, mes, anio);
                return await db.CuotasMensuales.FirstAsync(c => c.Id == cuota.Id);
            }
            catch (DbUpdateException) when (intento < 2)
            {
                db.Entry(cuota).State = EntityState.Detached;
                var existente = await db.CuotasMensuales
                    .FirstOrDefaultAsync(c => c.SocioId == socioId && c.Mes == mes && c.Anio == anio);
                if (existente != null)
                {
                    if (socio.FamiliaId.HasValue)
                        await NormalizarCuotasFamiliaAsync(socio.FamiliaId.Value, mes, anio);
                    return existente;
                }
            }
        }

        throw new InvalidOperationException("No se pudo crear la cuota mensual");
    }

    /// <summary>
    /// Una sola cuota cobrable por familia: titular lleva Familia.CuotaMensual; el resto MontoCuota=0.
    /// No multiplica el monto entre integrantes.
    /// </summary>
    public async Task NormalizarCuotasFamiliaAsync(int familiaId, int mes, int anio)
    {
        var familia = await db.Familias
            .Include(f => f.Socios)
            .FirstOrDefaultAsync(f => f.Id == familiaId);
        if (familia == null) return;

        var activos = familia.Socios
            .Where(s => s.Estado == EstadoSocio.Activo)
            .OrderBy(s => s.Id)
            .ToList();
        if (activos.Count == 0) return;

        var cuotas = new List<CuotaMensual>();
        foreach (var socio in activos)
        {
            var cuota = await db.CuotasMensuales
                .FirstOrDefaultAsync(c => c.SocioId == socio.Id && c.Mes == mes && c.Anio == anio);
            if (cuota == null)
            {
                cuota = new CuotaMensual
                {
                    EmisorId = socio.EmisorId,
                    SocioId = socio.Id,
                    Mes = mes,
                    Anio = anio,
                    MontoCuota = 0,
                    FechaVencimiento = UruguayTime.VencimientoCuota(mes, anio)
                };
                db.CuotasMensuales.Add(cuota);
                await db.SaveChangesAsync();
            }
            cuotas.Add(cuota);
        }

        var titular = cuotas.OrderBy(c => c.SocioId).First();
        var familiaYaPagada = cuotas.All(c => c.EstadoPago == EstadoPago.Pagado)
            || (titular.EstadoPago == EstadoPago.Pagado && titular.MontoCuota >= familia.CuotaMensual);

        foreach (var cuota in cuotas)
        {
            if (cuota.EstadoPago == EstadoPago.Pagado)
                continue;

            var esTitular = cuota.Id == titular.Id;
            cuota.MontoCuota = esTitular ? familia.CuotaMensual : 0m;
            RecalcularEstadoPago(cuota);
        }

        // Si la familia ya estaba cobrada, los integrantes con cuota 0 quedan cubiertos.
        if (familiaYaPagada)
        {
            foreach (var cuota in cuotas)
            {
                if (cuota.EstadoPago == EstadoPago.Pagado) continue;
                if (cuota.Total <= 0)
                {
                    cuota.EstadoPago = EstadoPago.Pagado;
                    cuota.FechaPago ??= DateTime.UtcNow;
                }
            }
        }

        await db.SaveChangesAsync();
    }

    public async Task NormalizarCuotasFamiliasDelMesAsync(int emisorId, int mes, int anio)
    {
        var familiaIds = await db.Socios
            .Where(s => s.EmisorId == emisorId && s.Estado == EstadoSocio.Activo && s.FamiliaId != null)
            .Select(s => s.FamiliaId!.Value)
            .Distinct()
            .ToListAsync();

        foreach (var familiaId in familiaIds)
            await NormalizarCuotasFamiliaAsync(familiaId, mes, anio);
    }

    public async Task ActualizarMontoServiciosAsync(int cuotaId)
    {
        var cuota = await db.CuotasMensuales
            .Include(c => c.Cargos)
            .Include(c => c.Socio)
            .FirstOrDefaultAsync(c => c.Id == cuotaId);

        if (cuota == null) return;

        var estabaPagada = cuota.EstadoPago == EstadoPago.Pagado;

        cuota.MontoServicios = cuota.Cargos
            .Where(c => c.SumarACuota && c.EstadoPago != EstadoPago.Anulado)
            .Sum(c => c.Monto * c.Cantidad);

        RecalcularEstadoPago(cuota, estabaPagada);
        await db.SaveChangesAsync();

        if (cuota.Socio.FamiliaId.HasValue)
            await NormalizarCuotasFamiliaAsync(cuota.Socio.FamiliaId.Value, cuota.Mes, cuota.Anio);
    }

    public static void RecalcularEstadoPago(CuotaMensual cuota, bool estabaPagada = false)
    {
        var total = cuota.Total;

        if (total <= 0)
        {
            // Integrante de familia sin monto propio: no fuerza "Pendiente" si ya estaba pagado.
            if (estabaPagada || cuota.MontoPagado > 0)
            {
                cuota.EstadoPago = EstadoPago.Pagado;
                cuota.FechaPago ??= DateTime.UtcNow;
            }
            else
                cuota.EstadoPago = EstadoPago.Pendiente;
            return;
        }

        if (cuota.MontoPagado >= total)
        {
            cuota.EstadoPago = EstadoPago.Pagado;
            cuota.FechaPago ??= DateTime.UtcNow;
            return;
        }

        if (cuota.MontoPagado <= 0)
        {
            cuota.EstadoPago = EstadoPago.Pendiente;
            return;
        }

        cuota.EstadoPago = estabaPagada ? EstadoPago.Pendiente : EstadoPago.Parcial;
    }

    public async Task MarcarCargosCuotaComoPagadosAsync(int cuotaId)
    {
        var cargos = await db.Cargos
            .Where(c => c.CuotaMensualId == cuotaId && c.SumarACuota && c.EstadoPago != EstadoPago.Anulado)
            .ToListAsync();

        foreach (var cargo in cargos)
            cargo.EstadoPago = EstadoPago.Pagado;
    }

    public async Task SincronizarCargosConEstadoCuotaAsync(int cuotaId)
    {
        var cuota = await db.CuotasMensuales.FirstOrDefaultAsync(c => c.Id == cuotaId);
        if (cuota == null) return;

        var cargos = await db.Cargos
            .Where(c => c.CuotaMensualId == cuotaId && c.SumarACuota && c.EstadoPago != EstadoPago.Anulado)
            .ToListAsync();

        foreach (var cargo in cargos)
        {
            cargo.EstadoPago = cuota.EstadoPago == EstadoPago.Pagado
                ? EstadoPago.Pagado
                : EstadoPago.Pendiente;
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Tras cobrar la familia: integrantes con saldo 0 quedan Pagado (acceso / ingreso).
    /// </summary>
    public async Task MarcarIntegrantesFamiliaCubiertosAsync(int familiaId, int mes, int anio)
    {
        var cuotas = await db.CuotasMensuales
            .Include(c => c.Socio)
            .Where(c => c.Socio.FamiliaId == familiaId && c.Mes == mes && c.Anio == anio)
            .ToListAsync();

        var titularPagado = cuotas
            .OrderBy(c => c.SocioId)
            .FirstOrDefault()?.EstadoPago == EstadoPago.Pagado;

        if (!titularPagado) return;

        foreach (var cuota in cuotas)
        {
            if (cuota.EstadoPago == EstadoPago.Pagado) continue;
            var saldo = cuota.Total - cuota.MontoPagado;
            if (saldo <= 0)
            {
                cuota.EstadoPago = EstadoPago.Pagado;
                cuota.FechaPago ??= DateTime.UtcNow;
                await MarcarCargosCuotaComoPagadosAsync(cuota.Id);
            }
        }

        await db.SaveChangesAsync();
    }
}
