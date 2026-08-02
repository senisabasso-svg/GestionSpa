using GestionSpa.Api.Data;
using GestionSpa.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionSpa.Api.Services;

public class IngresoAccesoService(AppDbContext db, CuotaService cuotaService)
{
    public record ResultadoAcceso(bool Permitido, string? MotivoRechazo, CuotaMensual? Cuota);

    public async Task<ResultadoAcceso> EvaluarAccesoSocioAsync(Socio socio)
    {
        if (socio.Estado != EstadoSocio.Activo)
            return new ResultadoAcceso(false, $"Socio {socio.Estado.ToString().ToLower()}", null);

        if (socio.FechaVencimiento.HasValue && socio.FechaVencimiento.Value.Date < UruguayTime.Today)
            return new ResultadoAcceso(false, "Membresía vencida", null);

        var (mes, anio) = UruguayTime.MesAnioActual();
        var cuota = await cuotaService.ObtenerOCrearCuotaAsync(socio.Id, mes, anio);

        if (!UruguayTime.EsDespuesDelDia10())
            return new ResultadoAcceso(true, null, cuota);

        if (socio.FamiliaId.HasValue)
        {
            await cuotaService.NormalizarCuotasFamiliaAsync(socio.FamiliaId.Value, mes, anio);
            var cuotasFamilia = await db.CuotasMensuales
                .Include(c => c.Socio)
                .Where(c => c.Mes == mes && c.Anio == anio
                            && c.Socio.FamiliaId == socio.FamiliaId
                            && c.Socio.Estado == EstadoSocio.Activo)
                .ToListAsync();

            var saldoFamilia = cuotasFamilia.Sum(c => c.Total - c.MontoPagado);
            var familiaPagada = cuotasFamilia.Count > 0
                && (saldoFamilia <= 0 || cuotasFamilia.All(c => c.EstadoPago == EstadoPago.Pagado));

            if (!familiaPagada)
                return new ResultadoAcceso(false, "Cuota familiar del mes pendiente de pago", cuota);

            return new ResultadoAcceso(true, null, cuota);
        }

        if (cuota.EstadoPago != EstadoPago.Pagado)
            return new ResultadoAcceso(false, "Cuota del mes pendiente de pago", cuota);

        if (cuota.MontoPagado < cuota.Total)
            return new ResultadoAcceso(false, "Saldo de cuota pendiente", cuota);

        return new ResultadoAcceso(true, null, cuota);
    }
}
