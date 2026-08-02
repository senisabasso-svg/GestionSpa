using GestionSpa.Api.DTOs;
using GestionSpa.Api.Models;
using GestionSpa.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GestionSpa.Api.Data;

namespace GestionSpa.Api.Controllers;

[ApiController]
[Route("api/portero")]
[Authorize(Roles = nameof(RolUsuario.AdminEmisor))]
public class PorteroController(
    AppDbContext db,
    ITenantContext tenant,
    IPorteroIntegrationService portero) : ControllerBase
{
    [HttpGet("config")]
    public async Task<ActionResult<PorteroConfigDto>> GetConfig()
    {
        var emisor = await RequireEmisorAsync();
        if (emisor is null) return Forbid();

        return await portero.GetConfigDtoAsync(emisor.Id, emisor.Slug, GetApiPublicBaseUrl());
    }

    [HttpPut("config")]
    public async Task<ActionResult<PorteroConfigDto>> SaveConfig(GuardarPorteroConfigDto dto)
    {
        var emisor = await RequireConfigUiAsync();
        if (emisor is null) return Forbid();

        if (dto.Habilitado && string.IsNullOrWhiteSpace(dto.ApiKey))
            return BadRequest(new { mensaje = "La API Key es obligatoria cuando el portero está habilitado", errores = new[] { "La API Key es obligatoria" } });

        return await portero.SaveConfigAsync(emisor.Id, emisor.Slug, dto, GetApiPublicBaseUrl());
    }

    [HttpPost("probar")]
    public async Task<ActionResult<PorteroPruebaConexionDto>> ProbarConexion()
    {
        var emisor = await RequireConfigUiAsync();
        if (emisor is null) return Forbid();

        return await portero.TestConnectionAsync(emisor.Id);
    }

    [HttpPost("sincronizar")]
    public async Task<ActionResult<PorteroSincronizacionDto>> Sincronizar()
    {
        var emisor = await RequireEmisorAsync();
        if (emisor is null) return Forbid();

        return await portero.SyncAllActiveSociosAsync(emisor.Id);
    }

    [HttpPost("abrir-puerta")]
    public async Task<ActionResult<PorteroAccionDto>> AbrirPuerta()
    {
        var emisor = await RequireEmisorAsync();
        if (emisor is null) return Forbid();

        try
        {
            return await portero.AbrirPuertaAsync(emisor.Id);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message, errores = new[] { ex.Message } });
        }
    }

    [HttpGet("exportar-socios")]
    public async Task<IActionResult> ExportarSocios()
    {
        var emisor = await RequireEmisorAsync();
        if (emisor is null) return Forbid();

        try
        {
            var (content, fileName) = await portero.ExportSociosPorteroCsvAsync(emisor.Id, emisor.Slug);
            return File(content, "text/csv; charset=utf-8", fileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message, errores = new[] { ex.Message } });
        }
    }

    private async Task<Emisor?> RequireEmisorAsync()
    {
        if (!tenant.EmisorId.HasValue) return null;
        return await db.Emisores.FirstOrDefaultAsync(e => e.Id == tenant.EmisorId && e.Activo);
    }

    private async Task<Emisor?> RequireConfigUiAsync()
    {
        var emisor = await RequireEmisorAsync();
        if (emisor is null || !emisor.MostrarConfigPortero) return null;
        return emisor;
    }

    private string? GetApiPublicBaseUrl()
    {
        var env = Environment.GetEnvironmentVariable("API_PUBLIC_BASE_URL");
        if (!string.IsNullOrWhiteSpace(env)) return env.Trim().TrimEnd('/');
        return $"{Request.Scheme}://{Request.Host}";
    }
}
