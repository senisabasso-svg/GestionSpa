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
            SocioId = socioId,
            Mes = mes,
            Anio = anio,
            MontoCuota = socio.CuotaMensual,
            FechaVencimiento = new DateTime(anio, mes, 10, 0, 0, 0, DateTimeKind.Utc)
        };

        db.CuotasMensuales.Add(cuota);
        await db.SaveChangesAsync();
        return cuota;
    }

    public async Task ActualizarMontoServiciosAsync(int cuotaId)
    {
        var cuota = await db.CuotasMensuales
            .Include(c => c.Cargos)
            .FirstOrDefaultAsync(c => c.Id == cuotaId);

        if (cuota == null) return;

        cuota.MontoServicios = cuota.Cargos
            .Where(c => c.SumarACuota)
            .Sum(c => c.Monto * c.Cantidad);

        var total = cuota.Total;
        if (total <= 0)
            cuota.EstadoPago = EstadoPago.Pendiente;
        else if (cuota.MontoPagado >= total)
            cuota.EstadoPago = EstadoPago.Pagado;
        else if (cuota.MontoPagado > 0)
            cuota.EstadoPago = EstadoPago.Parcial;
        else
            cuota.EstadoPago = EstadoPago.Pendiente;

        await db.SaveChangesAsync();
    }
}
