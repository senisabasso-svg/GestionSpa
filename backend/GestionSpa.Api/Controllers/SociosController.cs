using GestionSpa.Api.Data;
using GestionSpa.Api.DTOs;
using GestionSpa.Api.Models;
using GestionSpa.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionSpa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SociosController(AppDbContext db, CuotaService cuotaService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SocioDto>>> GetAll([FromQuery] string? buscar, [FromQuery] EstadoSocio? estado)
    {
        var query = db.Socios.AsQueryable();

        var term = ValidationHelper.SanitizeSearchTerm(buscar);
        if (term != null)
        {
            var lower = term.ToLower();
            query = query.Where(s =>
                s.NumeroSocio.Contains(term) ||
                s.Nombre.ToLower().Contains(lower) ||
                s.Apellido.ToLower().Contains(lower) ||
                s.Cedula.Contains(term));
        }

        if (estado.HasValue)
            query = query.Where(s => s.Estado == estado);

        var socios = await query.OrderBy(s => s.NumeroSocio).ToListAsync();
        return socios.Select(Map).ToList();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SocioDto>> GetById(int id)
    {
        var socio = await db.Socios.FindAsync(id);
        return socio == null ? NotFound() : Map(socio);
    }

    [HttpGet("numero/{numero}")]
    public async Task<ActionResult<SocioDto>> GetByNumero(string numero)
    {
        var socio = await db.Socios.FirstOrDefaultAsync(s => s.NumeroSocio == numero);
        return socio == null ? NotFound() : Map(socio);
    }

    [HttpPost]
    public async Task<ActionResult<SocioDto>> Create(CrearSocioDto dto)
    {
        var errors = ValidationHelper.ValidateSocio(
            dto.Nombre, dto.Apellido, dto.Cedula, dto.Telefono, dto.Email,
            dto.FechaAlta, dto.FechaVencimiento, dto.CuotaMensual);
        if (errors.Count > 0) return ValidationHelper.ToBadRequest(errors);

        if (await db.Socios.AnyAsync(s => s.Cedula == dto.Cedula))
            return BadRequest(new { mensaje = "Ya existe un socio con esa cédula", errores = new[] { "Ya existe un socio con esa cédula" } });

        var numeroSocio = await GenerarNumeroSocioAsync();

        var socio = new Socio
        {
            NumeroSocio = numeroSocio,
            Nombre = dto.Nombre.Trim(),
            Apellido = dto.Apellido.Trim(),
            Cedula = dto.Cedula.Trim(),
            Telefono = dto.Telefono?.Trim(),
            Email = dto.Email?.Trim(),
            FechaAlta = dto.FechaAlta.ToUniversalTime(),
            FechaVencimiento = dto.FechaVencimiento?.ToUniversalTime(),
            MedioPago = dto.MedioPago,
            CuotaMensual = dto.CuotaMensual,
            Ciudad = "Salto"
        };

        db.Socios.Add(socio);
        await db.SaveChangesAsync();

        var ahora = DateTime.UtcNow;
        await cuotaService.ObtenerOCrearCuotaAsync(socio.Id, ahora.Month, ahora.Year);

        return CreatedAtAction(nameof(GetById), new { id = socio.Id }, Map(socio));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<SocioDto>> Update(int id, CrearSocioDto dto)
    {
        var socio = await db.Socios.FindAsync(id);
        if (socio == null) return NotFound();

        var errors = ValidationHelper.ValidateSocio(
            dto.Nombre, dto.Apellido, dto.Cedula, dto.Telefono, dto.Email,
            dto.FechaAlta, dto.FechaVencimiento, dto.CuotaMensual);
        if (errors.Count > 0) return ValidationHelper.ToBadRequest(errors);

        if (await db.Socios.AnyAsync(s => s.Cedula == dto.Cedula && s.Id != id))
            return BadRequest(new { mensaje = "Ya existe otro socio con esa cédula", errores = new[] { "Ya existe otro socio con esa cédula" } });

        socio.Nombre = dto.Nombre.Trim();
        socio.Apellido = dto.Apellido.Trim();
        socio.Cedula = dto.Cedula.Trim();
        socio.Telefono = dto.Telefono?.Trim();
        socio.Email = dto.Email?.Trim();
        socio.FechaAlta = dto.FechaAlta.ToUniversalTime();
        socio.FechaVencimiento = dto.FechaVencimiento?.ToUniversalTime();
        socio.MedioPago = dto.MedioPago;
        socio.CuotaMensual = dto.CuotaMensual;

        await db.SaveChangesAsync();
        return Map(socio);
    }

    [HttpPatch("{id}/estado")]
    public async Task<ActionResult<SocioDto>> CambiarEstado(int id, [FromBody] EstadoSocio estado)
    {
        var socio = await db.Socios.FindAsync(id);
        if (socio == null) return NotFound();
        socio.Estado = estado;
        await db.SaveChangesAsync();
        return Map(socio);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var socio = await db.Socios.FindAsync(id);
        if (socio == null) return NotFound();
        socio.Estado = EstadoSocio.Inactivo;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<string> GenerarNumeroSocioAsync()
    {
        var numeros = await db.Socios.Select(s => s.NumeroSocio).ToListAsync();
        var max = numeros
            .Select(n => int.TryParse(n, out var v) ? v : 0)
            .DefaultIfEmpty(1000)
            .Max();
        return (max + 1).ToString();
    }

    private static SocioDto Map(Socio s) => new(
        s.Id, s.NumeroSocio, s.Nombre, s.Apellido, s.Cedula,
        s.Telefono, s.Email, s.Direccion, s.Ciudad,
        s.FechaAlta, s.FechaVencimiento, s.MedioPago,
        s.CuotaMensual, s.Estado, s.Observaciones);
}
