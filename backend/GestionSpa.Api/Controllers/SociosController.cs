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
public class SociosController(AppDbContext db, CuotaService cuotaService, ITenantContext tenant) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SocioDto>>> GetAll([FromQuery] string? buscar, [FromQuery] EstadoSocio? estado)
    {
        var query = db.Socios.ForTenant(tenant).Include(s => s.Familia).AsQueryable();

        if (estado.HasValue)
            query = query.Where(s => s.Estado == estado);

        var socios = await query.OrderBy(s => s.NumeroSocio).ToListAsync();

        var term = ValidationHelper.SanitizeSearchTerm(buscar);
        if (term != null)
            socios = socios.Where(s => ValidationHelper.MatchesSearch(term, s.NumeroSocio, s.Nombre, s.Apellido, s.Cedula, s.Ciudad)).ToList();

        return socios.Select(Map).ToList();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SocioDto>> GetById(int id)
    {
        var socio = await db.Socios.ForTenant(tenant).Include(s => s.Familia).FirstOrDefaultAsync(s => s.Id == id);
        return socio == null ? NotFound() : Map(socio);
    }

    [HttpGet("numero/{numero}")]
    public async Task<ActionResult<SocioDto>> GetByNumero(string numero)
    {
        var socio = await db.Socios.ForTenant(tenant).Include(s => s.Familia)
            .FirstOrDefaultAsync(s => s.NumeroSocio == numero);
        return socio == null ? NotFound() : Map(socio);
    }

    [HttpPost]
    public async Task<ActionResult<SocioDto>> Create(CrearSocioDto dto)
    {
        var emisorId = tenant.RequireEmisorId();
        var errors = ValidationHelper.ValidateSocio(
            dto.Nombre, dto.Apellido, dto.Cedula, dto.TipoIdentificacion,
            dto.Telefono, dto.Email, dto.Localidad,
            dto.FechaAlta, dto.FechaVencimiento, dto.CuotaMensual, esAlta: true);
        if (errors.Count > 0) return ValidationHelper.ToBadRequest(errors);

        if (await db.Socios.ForTenant(tenant).AnyAsync(s => s.Cedula == dto.Cedula))
            return BadRequest(new { mensaje = "Ya existe un socio con ese documento", errores = new[] { "Ya existe un socio con ese documento" } });

        if (dto.FamiliaId.HasValue && !await db.Familias.ForTenant(tenant).AnyAsync(f => f.Id == dto.FamiliaId))
            return BadRequest(new { mensaje = "La familia seleccionada no existe", errores = new[] { "La familia seleccionada no existe" } });

        var socio = new Socio
        {
            EmisorId = emisorId,
            NumeroSocio = await GenerarNumeroSocioAsync(emisorId),
            Nombre = dto.Nombre.Trim(),
            Apellido = dto.Apellido.Trim(),
            Cedula = dto.Cedula.Trim(),
            TipoIdentificacion = dto.TipoIdentificacion,
            Telefono = dto.Telefono?.Trim(),
            Email = dto.Email?.Trim(),
            FechaAlta = dto.FechaAlta.ToUniversalTime(),
            FechaVencimiento = dto.FechaVencimiento?.ToUniversalTime(),
            MedioPago = dto.MedioPago,
            CuotaMensual = dto.CuotaMensual,
            FamiliaId = dto.FamiliaId,
            Estado = dto.Estado,
            Ciudad = dto.Localidad!.Trim(),
        };

        db.Socios.Add(socio);
        await db.SaveChangesAsync();
        await db.Entry(socio).Reference(s => s.Familia).LoadAsync();

        if (socio.Estado == EstadoSocio.Activo)
        {
            var (mes, anio) = UruguayTime.MesAnioActual();
            await cuotaService.ObtenerOCrearCuotaAsync(socio.Id, mes, anio);
        }

        return CreatedAtAction(nameof(GetById), new { id = socio.Id }, Map(socio));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<SocioDto>> Update(int id, CrearSocioDto dto)
    {
        var socio = await db.Socios.ForTenant(tenant).Include(s => s.Familia).FirstOrDefaultAsync(s => s.Id == id);
        if (socio == null) return NotFound();

        var errors = ValidationHelper.ValidateSocio(
            dto.Nombre, dto.Apellido, dto.Cedula, dto.TipoIdentificacion,
            dto.Telefono, dto.Email, dto.Localidad,
            dto.FechaAlta, dto.FechaVencimiento, dto.CuotaMensual, esAlta: false);
        if (errors.Count > 0) return ValidationHelper.ToBadRequest(errors);

        if (await db.Socios.ForTenant(tenant).AnyAsync(s => s.Cedula == dto.Cedula && s.Id != id))
            return BadRequest(new { mensaje = "Ya existe otro socio con ese documento", errores = new[] { "Ya existe otro socio con ese documento" } });

        if (dto.FamiliaId.HasValue && !await db.Familias.ForTenant(tenant).AnyAsync(f => f.Id == dto.FamiliaId))
            return BadRequest(new { mensaje = "La familia seleccionada no existe", errores = new[] { "La familia seleccionada no existe" } });

        socio.Nombre = dto.Nombre.Trim();
        socio.Apellido = dto.Apellido.Trim();
        socio.Cedula = dto.Cedula.Trim();
        socio.TipoIdentificacion = dto.TipoIdentificacion;
        socio.Telefono = dto.Telefono?.Trim();
        socio.Email = dto.Email?.Trim();
        socio.FechaAlta = dto.FechaAlta.ToUniversalTime();
        socio.FechaVencimiento = dto.FechaVencimiento?.ToUniversalTime();
        var cuotaAnterior = socio.CuotaMensual;
        socio.MedioPago = dto.MedioPago;
        socio.CuotaMensual = dto.CuotaMensual;
        socio.FamiliaId = dto.FamiliaId;
        socio.Estado = dto.Estado;
        if (!string.IsNullOrWhiteSpace(dto.Localidad))
            socio.Ciudad = dto.Localidad.Trim();

        await db.SaveChangesAsync();

        if (cuotaAnterior != dto.CuotaMensual)
        {
            var (mes, anio) = UruguayTime.MesAnioActual();
            var cuotaMes = await db.CuotasMensuales.ForTenant(tenant)
                .FirstOrDefaultAsync(c => c.SocioId == id && c.Mes == mes && c.Anio == anio);

            if (cuotaMes != null && cuotaMes.EstadoPago != EstadoPago.Pagado)
            {
                cuotaMes.MontoCuota = dto.CuotaMensual;
                CuotaService.RecalcularEstadoPago(cuotaMes);
                await db.SaveChangesAsync();
            }
        }

        await db.Entry(socio).Reference(s => s.Familia).LoadAsync();
        return Map(socio);
    }

    [HttpPatch("{id}/estado")]
    public async Task<ActionResult<SocioDto>> CambiarEstado(int id, [FromBody] EstadoSocio estado)
    {
        var socio = await db.Socios.ForTenant(tenant).Include(s => s.Familia).FirstOrDefaultAsync(s => s.Id == id);
        if (socio == null) return NotFound();
        socio.Estado = estado;
        await db.SaveChangesAsync();
        return Map(socio);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var socio = await db.Socios.ForTenant(tenant).FirstOrDefaultAsync(s => s.Id == id);
        if (socio == null) return NotFound();
        socio.Estado = EstadoSocio.Inactivo;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<string> GenerarNumeroSocioAsync(int emisorId)
    {
        var numeros = await db.Socios.Where(s => s.EmisorId == emisorId).Select(s => s.NumeroSocio).ToListAsync();
        var max = numeros
            .Select(n => int.TryParse(n, out var v) ? v : 0)
            .DefaultIfEmpty(1000)
            .Max();
        return (max + 1).ToString();
    }

    private static SocioDto Map(Socio s) => new(
        s.Id, s.NumeroSocio, s.Nombre, s.Apellido, s.Cedula, s.TipoIdentificacion,
        s.Telefono, s.Email, s.Direccion, s.Ciudad,
        s.FechaAlta, s.FechaVencimiento, s.MedioPago,
        s.CuotaMensual, s.Estado, s.Observaciones,
        s.FamiliaId, s.Familia?.Nombre);
}
