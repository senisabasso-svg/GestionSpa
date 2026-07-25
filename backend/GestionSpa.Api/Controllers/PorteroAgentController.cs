using GestionSpa.Api.DTOs;
using GestionSpa.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionSpa.Api.Controllers;

/// <summary>
/// Endpoints que llama ApiPorteroSpa (PC del spa) hacia GestionSpa — modo pull.
/// Auth: header X-API-Key = la misma key guardada en Configuración → Portero.
/// </summary>
[ApiController]
[Route("api/portero/agent/{emisorSlug}")]
[AllowAnonymous]
public class PorteroAgentController(IPorteroIntegrationService portero) : ControllerBase
{
    [HttpGet("comandos")]
    public async Task<ActionResult<object>> Pull(string emisorSlug, [FromQuery] int limit = 50)
    {
        try
        {
            var key = GetApiKey();
            if (key is null) return Unauthorized(new { mensaje = "Falta header X-API-Key" });
            var comandos = await portero.PullComandosAsync(emisorSlug, key, limit);
            return Ok(new { comandos });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { mensaje = ex.Message });
        }
    }

    [HttpPost("comandos/{id:long}/ack")]
    public async Task<IActionResult> Ack(string emisorSlug, long id, [FromBody] PorteroAgentAckDto ack)
    {
        try
        {
            var key = GetApiKey();
            if (key is null) return Unauthorized(new { mensaje = "Falta header X-API-Key" });
            await portero.AckComandoAsync(emisorSlug, key, id, ack);
            return Ok(new { ok = true });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { mensaje = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }

    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat(string emisorSlug, [FromBody] PorteroAgentHeartbeatDto? dto)
    {
        try
        {
            var key = GetApiKey();
            if (key is null) return Unauthorized(new { mensaje = "Falta header X-API-Key" });
            await portero.HeartbeatAsync(emisorSlug, key, dto);
            return Ok(new { ok = true });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { mensaje = ex.Message });
        }
    }

    private string? GetApiKey()
    {
        if (Request.Headers.TryGetValue("X-API-Key", out var key) && !string.IsNullOrWhiteSpace(key))
            return key.ToString().Trim();
        return null;
    }
}
