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
public class UsuariosController(AppDbContext db, ITenantContext tenant) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<UsuarioDto>>> GetAll([FromQuery] int? emisorId)
    {
        var query = db.Usuarios.Include(u => u.Emisor).AsQueryable();

        if (tenant.IsSuperAdmin)
        {
            if (emisorId.HasValue)
                query = query.Where(u => u.EmisorId == emisorId);
        }
        else
        {
            if (tenant.Rol != RolUsuario.AdminEmisor)
                return Forbid();
            query = query.Where(u => u.EmisorId == tenant.EmisorId);
        }

        var usuarios = await query.OrderBy(u => u.Nombre).ToListAsync();
        return usuarios.Select(Map).ToList();
    }

    [Authorize(Roles = $"{nameof(RolUsuario.SuperAdmin)},{nameof(RolUsuario.AdminEmisor)}")]
    [HttpPost]
    public async Task<ActionResult<UsuarioDto>> Create(CrearUsuarioDto dto)
    {
        var errors = ValidateUsuario(dto);
        if (errors.Count > 0) return ValidationHelper.ToBadRequest(errors);

        var emisorId = ResolveEmisorId(dto);
        if (emisorId == null) return BadRequest(new { mensaje = "Emisor inválido" });
        if (!tenant.IsSuperAdmin && tenant.EmisorId != emisorId) return Forbid();
        if (dto.Rol == RolUsuario.SuperAdmin && !tenant.IsSuperAdmin) return Forbid();
        if (dto.Rol == RolUsuario.SuperAdmin) emisorId = null;

        var email = dto.Email.Trim().ToLowerInvariant();
        if (await db.Usuarios.AnyAsync(u => u.Email == email))
            return BadRequest(new { mensaje = "Ya existe un usuario con ese email" });

        if (emisorId.HasValue && !await db.Emisores.AnyAsync(e => e.Id == emisorId))
            return BadRequest(new { mensaje = "El emisor no existe" });

        var usuario = new Usuario
        {
            Email = email,
            PasswordHash = PasswordHasher.Hash(dto.Password),
            Nombre = dto.Nombre.Trim(),
            Rol = dto.Rol,
            EmisorId = emisorId,
        };

        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        await db.Entry(usuario).Reference(u => u.Emisor).LoadAsync();
        return CreatedAtAction(nameof(GetAll), Map(usuario));
    }

    [Authorize(Roles = $"{nameof(RolUsuario.SuperAdmin)},{nameof(RolUsuario.AdminEmisor)}")]
    [HttpPatch("{id}/activo")]
    public async Task<ActionResult<UsuarioDto>> ToggleActivo(int id, [FromBody] bool activo)
    {
        var usuario = await db.Usuarios.Include(u => u.Emisor).FirstOrDefaultAsync(u => u.Id == id);
        if (usuario == null) return NotFound();
        if (!CanManage(usuario)) return Forbid();
        if (usuario.Id == tenant.UserId) return BadRequest(new { mensaje = "No podés desactivar tu propio usuario" });

        usuario.Activo = activo;
        await db.SaveChangesAsync();
        return Map(usuario);
    }

    private bool CanManage(Usuario usuario)
    {
        if (tenant.IsSuperAdmin) return true;
        return tenant.Rol == RolUsuario.AdminEmisor && usuario.EmisorId == tenant.EmisorId;
    }

    private int? ResolveEmisorId(CrearUsuarioDto dto)
    {
        if (dto.Rol == RolUsuario.SuperAdmin) return null;
        if (tenant.IsSuperAdmin) return dto.EmisorId;
        return tenant.EmisorId;
    }

    private static List<string> ValidateUsuario(CrearUsuarioDto dto)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(dto.Email)) errors.Add("El email es obligatorio");
        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
            errors.Add("La contraseña debe tener al menos 6 caracteres");
        if (string.IsNullOrWhiteSpace(dto.Nombre)) errors.Add("El nombre es obligatorio");
        if (dto.Rol != RolUsuario.SuperAdmin && !dto.EmisorId.HasValue)
            errors.Add("Debés seleccionar un emisor");
        return errors;
    }

    private static UsuarioDto Map(Usuario u) => new(
        u.Id, u.Email, u.Nombre, u.Rol, u.EmisorId, u.Emisor?.Nombre, u.Activo);
}
