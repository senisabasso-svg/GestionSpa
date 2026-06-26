using GestionSpa.Api.Data;
using GestionSpa.Api.DTOs;
using GestionSpa.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionSpa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SociosController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SocioDto>>> GetAll([FromQuery] string? buscar, [FromQuery] EstadoSocio? estado)
    {
        var query = db.Socios.AsQueryable();

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var term = buscar.ToLower();
            query = query.Where(s =>
                s.NumeroSocio.Contains(term) ||
                s.Nombre.ToLower().Contains(term) ||
                s.Apellido.ToLower().Contains(term) ||
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
        if (await db.Socios.AnyAsync(s => s.Cedula == dto.Cedula))
            return BadRequest(new { mensaje = "Ya existe un socio con esa cédula" });

        var numeroSocio = await GenerarNumeroSocioAsync();

        var socio = new Socio
        {
            NumeroSocio = numeroSocio,
            Nombre = dto.Nombre,
            Apellido = dto.Apellido,
            Cedula = dto.Cedula,
            Telefono = dto.Telefono,
            Email = dto.Email,
            FechaAlta = dto.FechaAlta.ToUniversalTime(),
            FechaVencimiento = dto.FechaVencimiento?.ToUniversalTime(),
            MedioPago = dto.MedioPago,
            CuotaMensual = dto.CuotaMensual,
            Ciudad = "Salto"
        };

        db.Socios.Add(socio);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = socio.Id }, Map(socio));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<SocioDto>> Update(int id, CrearSocioDto dto)
    {
        var socio = await db.Socios.FindAsync(id);
        if (socio == null) return NotFound();

        if (await db.Socios.AnyAsync(s => s.Cedula == dto.Cedula && s.Id != id))
            return BadRequest(new { mensaje = "Ya existe otro socio con esa cédula" });

        socio.Nombre = dto.Nombre;
        socio.Apellido = dto.Apellido;
        socio.Cedula = dto.Cedula;
        socio.Telefono = dto.Telefono;
        socio.Email = dto.Email;
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
