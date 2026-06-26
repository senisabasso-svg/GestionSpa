using GestionSpa.Api.Data;
using GestionSpa.Api.DTOs;
using GestionSpa.Api.Models;
using GestionSpa.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionSpa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IngresosController(AppDbContext db, IngresoAccesoService accesoService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<IngresoDto>>> GetAll([FromQuery] DateTime? fecha)
    {
        var query = db.Ingresos.Include(i => i.Socio).AsQueryable();

        if (fecha.HasValue)
        {
            var inicio = UruguayTime.InicioDiaUtc(fecha.Value.Date);
            var fin = UruguayTime.FinDiaUtc(fecha.Value.Date);
            query = query.Where(i => i.FechaHora >= inicio && i.FechaHora < fin);
        }

        var ingresos = await query.OrderByDescending(i => i.FechaHora).Take(200).ToListAsync();
        return ingresos.Select(Map).ToList();
    }

    [HttpPost("validar")]
    public async Task<ActionResult<ResultadoIngresoDto>> ValidarIngreso(ValidarIngresoDto dto)
    {
        var socio = await db.Socios.FirstOrDefaultAsync(s => s.NumeroSocio == dto.NumeroSocio.Trim());

        if (socio == null)
        {
            return Ok(new ResultadoIngresoDto(
                false, "Número de socio no encontrado", null, dto.NumeroSocio, null, null));
        }

        var evaluacion = await accesoService.EvaluarAccesoSocioAsync(socio);

        if (!evaluacion.Permitido)
        {
            await RegistrarIngreso(socio.Id, false, evaluacion.MotivoRechazo);
            return Ok(new ResultadoIngresoDto(
                false, $"Acceso denegado: {evaluacion.MotivoRechazo}",
                $"{socio.Nombre} {socio.Apellido}", socio.NumeroSocio, socio.Estado,
                evaluacion.Cuota?.EstadoPago));
        }

        await RegistrarIngreso(socio.Id, true, null);
        return Ok(new ResultadoIngresoDto(
            true, $"¡Bienvenido/a, {socio.Nombre}!",
            $"{socio.Nombre} {socio.Apellido}", socio.NumeroSocio, socio.Estado,
            evaluacion.Cuota?.EstadoPago));
    }

    [HttpPost("salida")]
    public async Task<ActionResult<IngresoDto>> RegistrarSalida(ValidarIngresoDto dto)
    {
        var socio = await db.Socios.FirstOrDefaultAsync(s => s.NumeroSocio == dto.NumeroSocio.Trim());
        if (socio == null) return NotFound(new { mensaje = "Socio no encontrado" });

        var ingreso = new Ingreso
        {
            SocioId = socio.Id,
            Tipo = TipoIngreso.Salida,
            AccesoPermitido = true
        };

        db.Ingresos.Add(ingreso);
        await db.SaveChangesAsync();

        await db.Entry(ingreso).Reference(i => i.Socio).LoadAsync();
        return Map(ingreso);
    }

    private async Task RegistrarIngreso(int socioId, bool permitido, string? motivo)
    {
        if (socioId == 0) return;

        db.Ingresos.Add(new Ingreso
        {
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
