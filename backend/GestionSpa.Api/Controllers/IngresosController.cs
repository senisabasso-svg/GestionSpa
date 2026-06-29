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
public class IngresosController(AppDbContext db, IngresoAccesoService accesoService, ITenantContext tenant) : ControllerBase
{
    [Authorize]
    [HttpGet]
    public async Task<ActionResult<List<IngresoDto>>> GetAll([FromQuery] DateTime? fecha)
    {
        var query = db.Ingresos.ForTenant(tenant).Include(i => i.Socio).AsQueryable();

        if (fecha.HasValue)
        {
            var inicio = UruguayTime.InicioDiaUtc(fecha.Value.Date);
            var fin = UruguayTime.FinDiaUtc(fecha.Value.Date);
            query = query.Where(i => i.FechaHora >= inicio && i.FechaHora < fin);
        }

        var ingresos = await query.OrderByDescending(i => i.FechaHora).Take(200).ToListAsync();
        return ingresos.Select(Map).ToList();
    }

    [AllowAnonymous]
    [HttpPost("validar")]
    public async Task<ActionResult<ResultadoIngresoDto>> ValidarIngreso(ValidarIngresoDto dto)
    {
        var emisor = await ResolveEmisorAsync(dto.EmisorSlug);
        if (emisor == null)
            return Ok(new ResultadoIngresoDto(false, "Emisor no encontrado", null, dto.NumeroSocio, null, null));

        var socio = await db.Socios
            .FirstOrDefaultAsync(s => s.EmisorId == emisor.Id && s.NumeroSocio == dto.NumeroSocio.Trim());

        if (socio == null)
        {
            return Ok(new ResultadoIngresoDto(
                false, "Número de socio no encontrado", null, dto.NumeroSocio, null, null));
        }

        var evaluacion = await accesoService.EvaluarAccesoSocioAsync(socio);

        if (!evaluacion.Permitido)
        {
            await RegistrarIngreso(emisor.Id, socio.Id, false, evaluacion.MotivoRechazo);
            return Ok(new ResultadoIngresoDto(
                false, $"Acceso denegado: {evaluacion.MotivoRechazo}",
                $"{socio.Nombre} {socio.Apellido}", socio.NumeroSocio, socio.Estado,
                evaluacion.Cuota?.EstadoPago));
        }

        await RegistrarIngreso(emisor.Id, socio.Id, true, null);
        return Ok(new ResultadoIngresoDto(
            true, $"¡Bienvenido/a, {socio.Nombre}!",
            $"{socio.Nombre} {socio.Apellido}", socio.NumeroSocio, socio.Estado,
            evaluacion.Cuota?.EstadoPago));
    }

    [AllowAnonymous]
    [HttpPost("salida")]
    public async Task<ActionResult<IngresoDto>> RegistrarSalida(ValidarIngresoDto dto)
    {
        var emisor = await ResolveEmisorAsync(dto.EmisorSlug);
        if (emisor == null) return NotFound(new { mensaje = "Emisor no encontrado" });

        var socio = await db.Socios
            .FirstOrDefaultAsync(s => s.EmisorId == emisor.Id && s.NumeroSocio == dto.NumeroSocio.Trim());
        if (socio == null) return NotFound(new { mensaje = "Socio no encontrado" });

        var ingreso = new Ingreso
        {
            EmisorId = emisor.Id,
            SocioId = socio.Id,
            Tipo = TipoIngreso.Salida,
            AccesoPermitido = true
        };

        db.Ingresos.Add(ingreso);
        await db.SaveChangesAsync();

        await db.Entry(ingreso).Reference(i => i.Socio).LoadAsync();
        return Map(ingreso);
    }

    private async Task<Emisor?> ResolveEmisorAsync(string? slug)
    {
        if (!string.IsNullOrWhiteSpace(slug))
            return await db.Emisores.FirstOrDefaultAsync(e => e.Slug == slug.Trim().ToLowerInvariant() && e.Activo);

        if (tenant.EffectiveEmisorId.HasValue)
            return await db.Emisores.FindAsync(tenant.EffectiveEmisorId.Value);

        return null;
    }

    private async Task RegistrarIngreso(int emisorId, int socioId, bool permitido, string? motivo)
    {
        if (socioId == 0) return;

        db.Ingresos.Add(new Ingreso
        {
            EmisorId = emisorId,
            SocioId = socioId,
            AccesoPermitido = permitido,
            MotivoRechazo = motivo
        });
        await db.SaveChangesAsync();
    }

    private static IngresoDto Map(Ingreso i) => new(
        i.Id, i.SocioId, i.Socio?.NumeroSocio ?? "", i.Socio != null ? $"{i.Socio.Nombre} {i.Socio.Apellido}" : "",
        i.FechaHora, i.Tipo, i.AccesoPermitido, i.MotivoRechazo);
}
