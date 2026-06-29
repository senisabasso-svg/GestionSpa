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
public class AuthController(AppDbContext db, JwtTokenService jwt) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest(new { mensaje = "Email y contraseña son obligatorios" });

        var usuario = await db.Usuarios
            .Include(u => u.Emisor)
            .FirstOrDefaultAsync(u => u.Email == dto.Email.Trim().ToLowerInvariant());

        if (usuario == null || !usuario.Activo || !PasswordHasher.Verify(dto.Password, usuario.PasswordHash))
            return Unauthorized(new { mensaje = "Credenciales inválidas" });

        var token = jwt.GenerateToken(usuario);
        return new LoginResponseDto(
            token, usuario.Id, usuario.Email, usuario.Nombre,
            usuario.Rol, usuario.EmisorId,
            usuario.Emisor?.Nombre, usuario.Emisor?.Slug);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<LoginResponseDto>> Me()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userId, out var id)) return Unauthorized();

        var usuario = await db.Usuarios.Include(u => u.Emisor).FirstOrDefaultAsync(u => u.Id == id);
        if (usuario == null || !usuario.Activo) return Unauthorized();

        return new LoginResponseDto(
            "", usuario.Id, usuario.Email, usuario.Nombre,
            usuario.Rol, usuario.EmisorId,
            usuario.Emisor?.Nombre, usuario.Emisor?.Slug);
    }
}
