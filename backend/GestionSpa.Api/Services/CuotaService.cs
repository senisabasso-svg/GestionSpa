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

        var socio = await db.Socios.FindAsync(socioId)
            ?? throw new KeyNotFoundException("Socio no encontrado");

        cuota = new CuotaMensual
        {
            EmisorId = socio.EmisorId,
            SocioId = socioId,
            Mes = mes,
            Anio = anio,
            MontoCuota = socio.CuotaMensual,
            FechaVencimiento = UruguayTime.VencimientoCuota(mes, anio)
        };

        db.CuotasMensuales.Add(cuota);

        for (var intento = 0; intento < 3; intento++)
        {
            try
            {
                await db.SaveChangesAsync();
                return cuota;
            }
            catch (DbUpdateException) when (intento < 2)
            {
                db.Entry(cuota).State = EntityState.Detached;
                var existente = await db.CuotasMensuales
                    .FirstOrDefaultAsync(c => c.SocioId == socioId && c.Mes == mes && c.Anio == anio);
                if (existente != null) return existente;
            }
        }

        throw new InvalidOperationException("No se pudo crear la cuota mensual");
    }

    public async Task ActualizarMontoServiciosAsync(int cuotaId)
    {
        var cuota = await db.CuotasMensuales
            .Include(c => c.Cargos)
            .FirstOrDefaultAsync(c => c.Id == cuotaId);

        if (cuota == null) return;

        var estabaPagada = cuota.EstadoPago == EstadoPago.Pagado;

        cuota.MontoServicios = cuota.Cargos
            .Where(c => c.SumarACuota && c.EstadoPago != EstadoPago.Anulado)
            .Sum(c => c.Monto * c.Cantidad);

        RecalcularEstadoPago(cuota, estabaPagada);
        await db.SaveChangesAsync();
    }

    public static void RecalcularEstadoPago(CuotaMensual cuota, bool estabaPagada = false)
    {
        var total = cuota.Total;

        if (total <= 0)
        {
            cuota.EstadoPago = cuota.MontoPagado > 0 ? EstadoPago.Pagado : EstadoPago.Pendiente;
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
}
