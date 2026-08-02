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
public class PagosController(AppDbContext db, CuotaService cuotaService, ITenantContext tenant) : ControllerBase
{
    /// <summary>Ventana server-side un poco más amplia que los 10s de UI (latencia / reloj).</summary>
    private static readonly TimeSpan VentanaRevertir = TimeSpan.FromSeconds(20);

    [HttpPost("revertir")]
    public async Task<ActionResult<object>> Revertir([FromBody] RevertirPagosDto dto)
    {
        if (dto.Ids == null || dto.Ids.Count == 0)
            return BadRequest(new { mensaje = "No hay pagos para revertir", errores = new[] { "Ids vacíos" } });

        var ids = dto.Ids.Distinct().ToList();
        var pagos = await db.Pagos.ForTenant(tenant)
            .Include(p => p.CuotaMensual!).ThenInclude(c => c!.Socio)
            .Include(p => p.Cargo)
            .Where(p => ids.Contains(p.Id))
            .ToListAsync();

        if (pagos.Count == 0)
            return NotFound(new { mensaje = "No se encontraron los pagos" });

        var ahora = DateTime.UtcNow;
        foreach (var pago in pagos)
        {
            var edad = ahora - pago.Fecha.ToUniversalTime();
            if (edad > VentanaRevertir)
                return BadRequest(new
                {
                    mensaje = "Ya pasó el tiempo para revertir este pago (máx. ~10 segundos).",
                    errores = new[] { "Ventana de reversión expirada" }
                });
        }

        var cuotasAfectadas = new HashSet<int>();
        var cargosAfectados = new HashSet<int>();
        var familiasANormalizar = new List<(int FamiliaId, int Mes, int Anio)>();

        foreach (var pago in pagos)
        {
            if (pago.CuotaMensualId.HasValue && pago.CuotaMensual != null)
            {
                var cuota = pago.CuotaMensual;
                cuota.MontoPagado = Math.Max(0, cuota.MontoPagado - pago.Monto);
                CuotaService.RecalcularEstadoPago(cuota);
                if (cuota.EstadoPago != EstadoPago.Pagado)
                    cuota.FechaPago = null;
                cuotasAfectadas.Add(cuota.Id);
                if (cuota.Socio?.FamiliaId is int fid)
                    familiasANormalizar.Add((fid, cuota.Mes, cuota.Anio));
            }
            else if (pago.CargoId.HasValue)
            {
                cargosAfectados.Add(pago.CargoId.Value);
            }

            db.Pagos.Remove(pago);
        }

        await db.SaveChangesAsync();

        foreach (var cargoId in cargosAfectados)
        {
            var cargo = await db.Cargos.ForTenant(tenant).FirstOrDefaultAsync(c => c.Id == cargoId);
            if (cargo == null) continue;
            var montoTotal = cargo.Monto * cargo.Cantidad;
            var yaPagado = await db.Pagos.ForTenant(tenant)
                .Where(p => p.CargoId == cargo.Id)
                .SumAsync(p => p.Monto);
            cargo.EstadoPago = yaPagado >= montoTotal ? EstadoPago.Pagado
                : yaPagado > 0 ? EstadoPago.Parcial
                : EstadoPago.Pendiente;
        }

        await db.SaveChangesAsync();

        foreach (var cuotaId in cuotasAfectadas)
            await cuotaService.SincronizarCargosConEstadoCuotaAsync(cuotaId);

        foreach (var (familiaId, mes, anio) in familiasANormalizar.Distinct())
        {
            await DesmarcarIntegrantesSoloCubiertosAsync(familiaId, mes, anio);
            await cuotaService.NormalizarCuotasFamiliaAsync(familiaId, mes, anio);
        }

        return Ok(new
        {
            mensaje = pagos.Count == 1
                ? "Pago revertido."
                : $"Se revirtieron {pagos.Count} pagos.",
            revertidos = pagos.Count
        });
    }

    /// <summary>
    /// Integrantes con total 0 que se marcaron Pagado solo por cobro familiar vuelven a Pendiente.
    /// </summary>
    private async Task DesmarcarIntegrantesSoloCubiertosAsync(int familiaId, int mes, int anio)
    {
        var cuotas = await db.CuotasMensuales.ForTenant(tenant)
            .Include(c => c.Socio)
            .Where(c => c.Mes == mes && c.Anio == anio
                        && c.Socio.FamiliaId == familiaId
                        && c.Socio.Estado == EstadoSocio.Activo)
            .ToListAsync();

        foreach (var cuota in cuotas)
        {
            if (cuota.MontoPagado > 0) continue;
            if (cuota.Total <= 0 && cuota.EstadoPago == EstadoPago.Pagado)
            {
                cuota.EstadoPago = EstadoPago.Pendiente;
                cuota.FechaPago = null;
            }
        }

        await db.SaveChangesAsync();
    }
}
