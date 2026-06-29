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
public class ClientesController(AppDbContext db, ITenantContext tenant) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ClienteDto>>> GetAll([FromQuery] string? buscar)
    {
        var clientes = await db.Clientes.ForTenant(tenant).OrderByDescending(c => c.FechaRegistro).ToListAsync();
        var term = ValidationHelper.SanitizeSearchTerm(buscar);
        if (term != null)
            clientes = clientes.Where(c => ValidationHelper.MatchesSearch(term, c.Nombre, c.Apellido, c.Cedula)).ToList();

        return clientes.Select(Map).ToList();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ClienteDto>> GetById(int id)
    {
        var cliente = await db.Clientes.ForTenant(tenant).FirstOrDefaultAsync(c => c.Id == id);
        return cliente == null ? NotFound() : Map(cliente);
    }

    [HttpPost]
    public async Task<ActionResult<ClienteDto>> Create(CrearClienteDto dto)
    {
        var errors = ValidationHelper.ValidateCliente(dto.Nombre, dto.Apellido, dto.Email);
        if (errors.Count > 0) return ValidationHelper.ToBadRequest(errors);

        var cliente = new Cliente
        {
            EmisorId = tenant.RequireEmisorId(),
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
        var cliente = await db.Clientes.ForTenant(tenant).FirstOrDefaultAsync(c => c.Id == id);
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
        var cliente = await db.Clientes.ForTenant(tenant).FirstOrDefaultAsync(c => c.Id == id);
        if (cliente == null) return NotFound();

        if (await db.Cargos.ForTenant(tenant).AnyAsync(c => c.ClienteId == id))
            return BadRequest(new { mensaje = "No se puede eliminar: el cliente tiene cargos registrados" });

        db.Clientes.Remove(cliente);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static ClienteDto Map(Cliente c) => new(
        c.Id, c.Nombre, c.Apellido, c.Cedula, c.Telefono, c.Email, c.FechaRegistro);
}
