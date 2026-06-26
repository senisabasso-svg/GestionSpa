using GestionSpa.Api.Data;
using GestionSpa.Api.DTOs;
using GestionSpa.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionSpa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServiciosController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ServicioDto>>> GetAll([FromQuery] bool? activos, [FromQuery] CategoriaServicio? categoria)
    {
        var query = db.Servicios.AsQueryable();

        if (activos == true)
            query = query.Where(s => s.Activo);
        if (categoria.HasValue)
            query = query.Where(s => s.Categoria == categoria);

        var servicios = await query.OrderBy(s => s.Categoria).ThenBy(s => s.Nombre).ToListAsync();
        return servicios.Select(Map).ToList();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ServicioDto>> GetById(int id)
    {
        var servicio = await db.Servicios.FindAsync(id);
        return servicio == null ? NotFound() : Map(servicio);
    }

    [HttpPost]
    public async Task<ActionResult<ServicioDto>> Create(CrearServicioDto dto)
    {
        var servicio = new Servicio
        {
            Nombre = dto.Nombre,
            Descripcion = dto.Descripcion,
            Categoria = dto.Categoria,
            Precio = dto.Precio,
            DuracionMinutos = dto.DuracionMinutos,
            SoloSocios = dto.SoloSocios
        };

        db.Servicios.Add(servicio);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = servicio.Id }, Map(servicio));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ServicioDto>> Update(int id, CrearServicioDto dto)
    {
        var servicio = await db.Servicios.FindAsync(id);
        if (servicio == null) return NotFound();

        servicio.Nombre = dto.Nombre;
        servicio.Descripcion = dto.Descripcion;
        servicio.Categoria = dto.Categoria;
        servicio.Precio = dto.Precio;
        servicio.DuracionMinutos = dto.DuracionMinutos;
        servicio.SoloSocios = dto.SoloSocios;

        await db.SaveChangesAsync();
        return Map(servicio);
    }

    [HttpPatch("{id}/activo")]
    public async Task<ActionResult<ServicioDto>> ToggleActivo(int id)
    {
        var servicio = await db.Servicios.FindAsync(id);
        if (servicio == null) return NotFound();
        servicio.Activo = !servicio.Activo;
        await db.SaveChangesAsync();
        return Map(servicio);
    }

    private static ServicioDto Map(Servicio s) => new(
        s.Id, s.Nombre, s.Descripcion, s.Categoria,
        s.Precio, s.DuracionMinutos, s.Activo, s.SoloSocios);
}
