using System.Text.RegularExpressions;
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
public class EmisoresController(AppDbContext db, ITenantContext tenant) : ControllerBase
{
    private static readonly Regex SlugRegex = new(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);

    [AllowAnonymous]
    [HttpGet("publico/{slug}")]
    public async Task<ActionResult<EmisorPublicoDto>> GetPublico(string slug)
    {
        var emisor = await db.Emisores
            .FirstOrDefaultAsync(e => e.Slug == slug.Trim().ToLowerInvariant() && e.Activo);
        return emisor == null ? NotFound() : new EmisorPublicoDto(emisor.Id, emisor.Nombre, emisor.Slug, emisor.Ciudad);
    }

    [Authorize(Roles = nameof(RolUsuario.SuperAdmin))]
    [HttpGet]
    public async Task<ActionResult<List<EmisorDto>>> GetAll()
    {
        var emisores = await db.Emisores.OrderBy(e => e.Nombre).ToListAsync();
        return emisores.Select(Map).ToList();
    }

    [Authorize]
    [HttpGet("{id}")]
    public async Task<ActionResult<EmisorDto>> GetById(int id)
    {
        if (!tenant.IsSuperAdmin && tenant.EmisorId != id)
            return Forbid();

        var emisor = await db.Emisores.FindAsync(id);
        return emisor == null ? NotFound() : Map(emisor);
    }

    [Authorize(Roles = nameof(RolUsuario.SuperAdmin))]
    [HttpPost]
    public async Task<ActionResult<EmisorDto>> Create(CrearEmisorDto dto)
    {
        var errors = ValidateEmisor(dto.Nombre, dto.Slug);
        if (errors.Count > 0) return ValidationHelper.ToBadRequest(errors);

        var slug = dto.Slug.Trim().ToLowerInvariant();
        var nombre = dto.Nombre.Trim();

        if (await db.Emisores.AnyAsync(e => e.Slug == slug))
            return BadRequest(new { mensaje = "Ya existe un emisor con ese slug", errores = new[] { "Slug duplicado" } });
        if (await db.Emisores.AnyAsync(e => e.Nombre == nombre))
            return BadRequest(new { mensaje = "Ya existe un emisor con ese nombre", errores = new[] { "Nombre duplicado" } });

        var emisor = new Emisor
        {
            Nombre = nombre,
            Slug = slug,
            Ciudad = dto.Ciudad?.Trim(),
            Departamento = dto.Departamento?.Trim(),
        };

        db.Emisores.Add(emisor);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = emisor.Id }, Map(emisor));
    }

    [Authorize(Roles = nameof(RolUsuario.SuperAdmin))]
    [HttpPut("{id}")]
    public async Task<ActionResult<EmisorDto>> Update(int id, CrearEmisorDto dto)
    {
        var emisor = await db.Emisores.FindAsync(id);
        if (emisor == null) return NotFound();

        var errors = ValidateEmisor(dto.Nombre, dto.Slug);
        if (errors.Count > 0) return ValidationHelper.ToBadRequest(errors);

        var slug = dto.Slug.Trim().ToLowerInvariant();
        var nombre = dto.Nombre.Trim();

        if (await db.Emisores.AnyAsync(e => e.Slug == slug && e.Id != id))
            return BadRequest(new { mensaje = "Ya existe otro emisor con ese slug" });
        if (await db.Emisores.AnyAsync(e => e.Nombre == nombre && e.Id != id))
            return BadRequest(new { mensaje = "Ya existe otro emisor con ese nombre" });

        emisor.Nombre = nombre;
        emisor.Slug = slug;
        emisor.Ciudad = dto.Ciudad?.Trim();
        emisor.Departamento = dto.Departamento?.Trim();

        await db.SaveChangesAsync();
        return Map(emisor);
    }

    [Authorize(Roles = nameof(RolUsuario.SuperAdmin))]
    [HttpPatch("{id}/activo")]
    public async Task<ActionResult<EmisorDto>> ToggleActivo(int id, [FromBody] bool activo)
    {
        var emisor = await db.Emisores.FindAsync(id);
        if (emisor == null) return NotFound();
        emisor.Activo = activo;
        await db.SaveChangesAsync();
        return Map(emisor);
    }

    private static List<string> ValidateEmisor(string nombre, string slug)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(nombre)) errors.Add("El nombre es obligatorio");
        if (string.IsNullOrWhiteSpace(slug)) errors.Add("El slug es obligatorio");
        else if (!SlugRegex.IsMatch(slug.Trim().ToLowerInvariant()))
            errors.Add("El slug solo puede contener letras minúsculas, números y guiones");
        return errors;
    }

    private static EmisorDto Map(Emisor e) => new(
        e.Id, e.Nombre, e.Slug, e.Ciudad, e.Departamento, e.Activo, e.FechaAlta);
}
