using GestionSpa.Api.Data;
using GestionSpa.Api.DTOs;
using GestionSpa.Api.Models;
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

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var term = buscar.ToLower();
            query = query.Where(c =>
                c.Nombre.ToLower().Contains(term) ||
                c.Apellido.ToLower().Contains(term) ||
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
        var cliente = new Cliente
        {
            Nombre = dto.Nombre,
            Apellido = dto.Apellido,
            Cedula = dto.Cedula,
            Telefono = dto.Telefono,
            Email = dto.Email,
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

        cliente.Nombre = dto.Nombre;
        cliente.Apellido = dto.Apellido;
        cliente.Cedula = dto.Cedula;
        cliente.Telefono = dto.Telefono;
        cliente.Email = dto.Email;
        cliente.Observaciones = dto.Observaciones;

        await db.SaveChangesAsync();
        return Map(cliente);
    }

    private static ClienteDto Map(Cliente c) => new(
        c.Id, c.Nombre, c.Apellido, c.Cedula, c.Telefono, c.Email, c.FechaRegistro);
}
