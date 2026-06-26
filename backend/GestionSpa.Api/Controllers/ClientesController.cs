using GestionSpa.Api.Data;
using GestionSpa.Api.DTOs;
using GestionSpa.Api.Models;
using GestionSpa.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionSpa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ClienteDto>>> GetAll([FromQuery] string? buscar)
    {
        var query = db.Clientes.AsQueryable();
        var term = ValidationHelper.SanitizeSearchTerm(buscar);
        if (term != null)
        {
            var lower = term.ToLower();
            query = query.Where(c =>
                c.Nombre.ToLower().Contains(lower) ||
                c.Apellido.ToLower().Contains(lower) ||
                (c.Cedula != null && c.Cedula.Contains(term)));
        }

        var clientes = await query.OrderByDescending(c => c.FechaRegistro).ToListAsync();
        return clientes.Select(Map).ToList();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ClienteDto>> GetById(int id)
    {
        var cliente = await db.Clientes.FindAsync(id);
        return cliente == null ? NotFound() : Map(cliente);
    }

    [HttpPost]
    public async Task<ActionResult<ClienteDto>> Create(CrearClienteDto dto)
    {
        var errors = ValidationHelper.ValidateCliente(dto.Nombre, dto.Apellido, dto.Email);
        if (errors.Count > 0) return ValidationHelper.ToBadRequest(errors);

        var cliente = new Cliente
        {
            Nombre = dto.Nombre.Trim(),
            Apellido = dto.Apellido.Trim(),
            Cedula = dto.Cedula?.Trim(),
            Telefono = dto.Telefono?.Trim(),
            Email = dto.Email?.Trim(),
            Observaciones = dto.Observaciones
        };

        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = cliente.Id }, Map(cliente));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ClienteDto>> Update(int id, CrearClienteDto dto)
    {
        var cliente = await db.Clientes.FindAsync(id);
        if (cliente == null) return NotFound();

        var errors = ValidationHelper.ValidateCliente(dto.Nombre, dto.Apellido, dto.Email);
        if (errors.Count > 0) return ValidationHelper.ToBadRequest(errors);

        cliente.Nombre = dto.Nombre.Trim();
        cliente.Apellido = dto.Apellido.Trim();
        cliente.Cedula = dto.Cedula?.Trim();
        cliente.Telefono = dto.Telefono?.Trim();
        cliente.Email = dto.Email?.Trim();
        cliente.Observaciones = dto.Observaciones;

        await db.SaveChangesAsync();
        return Map(cliente);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var cliente = await db.Clientes.FindAsync(id);
        if (cliente == null) return NotFound();
        db.Clientes.Remove(cliente);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static ClienteDto Map(Cliente c) => new(
        c.Id, c.Nombre, c.Apellido, c.Cedula, c.Telefono, c.Email, c.FechaRegistro);
}
