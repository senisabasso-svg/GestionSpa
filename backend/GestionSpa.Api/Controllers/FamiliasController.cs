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
public class FamiliasController(AppDbContext db, ITenantContext tenant) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<FamiliaDto>>> GetAll([FromQuery] string? buscar)
    {
        var familias = await db.Familias.ForTenant(tenant)
            .Include(f => f.Socios)
            .OrderBy(f => f.Nombre)
            .ToListAsync();

        var term = ValidationHelper.SanitizeSearchTerm(buscar);
        if (term != null)
            familias = familias.Where(f => ValidationHelper.MatchesSearch(term, f.Nombre)).ToList();

        return familias.Select(Map).ToList();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FamiliaDto>> GetById(int id)
    {
        var familia = await db.Familias.ForTenant(tenant).Include(f => f.Socios).FirstOrDefaultAsync(f => f.Id == id);
        return familia == null ? NotFound() : Map(familia);
    }

    [HttpPost]
    public async Task<ActionResult<FamiliaDto>> Create(CrearFamiliaDto dto)
    {
        var emisorId = tenant.RequireEmisorId();
        var errors = ValidationHelper.ValidateFamilia(dto.Nombre, dto.CuotaMensual);
        if (errors.Count > 0) return ValidationHelper.ToBadRequest(errors);

        var nombre = dto.Nombre.Trim();
        if (await db.Familias.ForTenant(tenant).AnyAsync(f => f.Nombre == nombre))
            return BadRequest(new { mensaje = "Ya existe una familia con ese nombre", errores = new[] { "Ya existe una familia con ese nombre" } });

        var familia = new Familia
        {
            EmisorId = emisorId,
            Nombre = nombre,
            CuotaMensual = dto.CuotaMensual,
            Observaciones = dto.Observaciones?.Trim()
        };

        db.Familias.Add(familia);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = familia.Id }, Map(familia));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<FamiliaDto>> Update(int id, CrearFamiliaDto dto)
    {
        var familia = await db.Familias.ForTenant(tenant).Include(f => f.Socios).FirstOrDefaultAsync(f => f.Id == id);
        if (familia == null) return NotFound();

        var errors = ValidationHelper.ValidateFamilia(dto.Nombre, dto.CuotaMensual);
        if (errors.Count > 0) return ValidationHelper.ToBadRequest(errors);

        var nombre = dto.Nombre.Trim();
        if (await db.Familias.ForTenant(tenant).AnyAsync(f => f.Nombre == nombre && f.Id != id))
            return BadRequest(new { mensaje = "Ya existe otra familia con ese nombre", errores = new[] { "Ya existe otra familia con ese nombre" } });

        var cuotaAnterior = familia.CuotaMensual;
        familia.Nombre = nombre;
        familia.CuotaMensual = dto.CuotaMensual;
        familia.Observaciones = dto.Observaciones?.Trim();

        if (cuotaAnterior != dto.CuotaMensual && familia.Socios.Count > 0)
        {
            var (mes, anio) = UruguayTime.MesAnioActual();
            foreach (var socio in familia.Socios)
            {
                socio.CuotaMensual = dto.CuotaMensual;
                var cuotaMes = await db.CuotasMensuales.ForTenant(tenant)
                    .FirstOrDefaultAsync(c => c.SocioId == socio.Id && c.Mes == mes && c.Anio == anio);
                if (cuotaMes != null && cuotaMes.EstadoPago != EstadoPago.Pagado)
                {
                    cuotaMes.MontoCuota = dto.CuotaMensual;
                    CuotaService.RecalcularEstadoPago(cuotaMes);
                }
            }
        }

        await db.SaveChangesAsync();
        return Map(familia);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var familia = await db.Familias.ForTenant(tenant).Include(f => f.Socios).FirstOrDefaultAsync(f => f.Id == id);
        if (familia == null) return NotFound();

        if (familia.Socios.Any(s => s.Estado != EstadoSocio.Inactivo))
            return BadRequest(new { mensaje = "No se puede eliminar: la familia tiene socios asignados" });

        db.Familias.Remove(familia);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static FamiliaDto Map(Familia f) => new(
        f.Id, f.Nombre, f.CuotaMensual, f.Observaciones, f.Socios.Count);
}
