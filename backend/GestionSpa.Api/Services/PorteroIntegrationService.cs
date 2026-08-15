using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GestionSpa.Api.Data;
using GestionSpa.Api.DTOs;
using GestionSpa.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionSpa.Api.Services;

public interface IPorteroIntegrationService
{
    Task<EmisorPorteroConfig?> GetConfigAsync(int emisorId);
    Task<PorteroConfigDto> GetConfigDtoAsync(int emisorId, string emisorSlug, string? apiPublicBaseUrl);
    Task<PorteroConfigDto> SaveConfigAsync(int emisorId, string emisorSlug, GuardarPorteroConfigDto dto, string? apiPublicBaseUrl);
    Task<PorteroPruebaConexionDto> TestConnectionAsync(int emisorId);
    Task<PorteroSincronizacionDto> SyncAllActiveSociosAsync(int emisorId);
    Task TrySyncSocioAsync(Socio socio);
    Task TryRemoveSocioAsync(Socio socio);
    Task ProcessAccessWebhookAsync(string emisorSlug, PorteroWebhookPayload payload);
    Task<PorteroAccionDto> AbrirPuertaAsync(int emisorId);
    Task<(byte[] Content, string FileName)> ExportSociosPorteroCsvAsync(int emisorId, string emisorSlug);
    Task<(byte[] Content, string FileName)> ExportUsuariosExtraidosGuardadosCsvAsync(int emisorId, string emisorSlug);
    Task<PorteroAccionDto> CancelarConsultaUsuariosPorteroAsync(int emisorId);
    Task<List<PorteroAgentComandoDto>> PullComandosAsync(string emisorSlug, string apiKey, int limit = 50);
    Task AckComandoAsync(string emisorSlug, string apiKey, long comandoId, PorteroAgentAckDto ack);
    Task HeartbeatAsync(string emisorSlug, string apiKey, PorteroAgentHeartbeatDto? dto);
}

public class PorteroWebhookPayload
{
    public string Event { get; set; } = "";
    public JsonElement Data { get; set; }
}

public class PorteroIntegrationService(
    AppDbContext db,
    ILogger<PorteroIntegrationService> logger) : IPorteroIntegrationService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<EmisorPorteroConfig?> GetConfigAsync(int emisorId) =>
        await db.EmisorPorteroConfigs.FirstOrDefaultAsync(c => c.EmisorId == emisorId);

    public async Task<PorteroConfigDto> GetConfigDtoAsync(int emisorId, string emisorSlug, string? apiPublicBaseUrl)
    {
        var config = await GetConfigAsync(emisorId);
        var pendientes = await db.PorteroComandos.CountAsync(c =>
            c.EmisorId == emisorId &&
            (c.Estado == PorteroComandoEstado.Pendiente || c.Estado == PorteroComandoEstado.Procesando));
        return MapConfigDto(config, emisorSlug, apiPublicBaseUrl, pendientes);
    }

    public async Task<PorteroConfigDto> SaveConfigAsync(int emisorId, string emisorSlug, GuardarPorteroConfigDto dto, string? apiPublicBaseUrl)
    {
        var config = await db.EmisorPorteroConfigs.FirstOrDefaultAsync(c => c.EmisorId == emisorId);
        if (config is null)
        {
            config = new EmisorPorteroConfig { EmisorId = emisorId };
            db.EmisorPorteroConfigs.Add(config);
        }

        config.Habilitado = dto.Habilitado;
        // ApiUrl queda como nota opcional (modo pull: el agente llama a GestionSpa).
        config.ApiUrl = string.IsNullOrWhiteSpace(dto.ApiUrl) ? "pull" : dto.ApiUrl.Trim().TrimEnd('/');
        config.ApiKey = dto.ApiKey.Trim();
        config.WebhookSecret = string.IsNullOrWhiteSpace(dto.WebhookSecret) ? null : dto.WebhookSecret.Trim();
        config.DeviceSn = string.IsNullOrWhiteSpace(dto.DeviceSn) ? "7674222960189" : dto.DeviceSn.Trim();
        config.SincronizarAutomatico = dto.SincronizarAutomatico;
        config.FechaActualizacion = DateTime.UtcNow;

        await db.SaveChangesAsync();
        var pendientes = await db.PorteroComandos.CountAsync(c =>
            c.EmisorId == emisorId &&
            (c.Estado == PorteroComandoEstado.Pendiente || c.Estado == PorteroComandoEstado.Procesando));
        return MapConfigDto(config, emisorSlug, apiPublicBaseUrl, pendientes);
    }

    public async Task<PorteroPruebaConexionDto> TestConnectionAsync(int emisorId)
    {
        var config = await GetConfigAsync(emisorId);
        if (config is null || !config.Habilitado)
            return new PorteroPruebaConexionDto(false, "Portero no habilitado o sin configuración", null);

        var pendientes = await db.PorteroComandos.CountAsync(c =>
            c.EmisorId == emisorId && c.Estado == PorteroComandoEstado.Pendiente);

        if (config.UltimoHeartbeatUtc is null)
            return new PorteroPruebaConexionDto(false,
                "Sin heartbeat del agente. En la PC del spa configurá la URL de GestionSpa y dejá ApiPorteroSpa iniciado.",
                new { pendientes, modo = "pull" });

        var age = DateTime.UtcNow - config.UltimoHeartbeatUtc.Value;
        if (age > TimeSpan.FromMinutes(2))
            return new PorteroPruebaConexionDto(false,
                $"Agente sin contacto hace {(int)age.TotalMinutes} min. Revisá que ApiPorteroSpa esté corriendo e internet en la PC del spa.",
                new { pendientes, ultimoHeartbeatUtc = config.UltimoHeartbeatUtc, modo = "pull" });

        return new PorteroPruebaConexionDto(true,
            $"Agente online (último contacto hace {(int)age.TotalSeconds} s). Comandos pendientes: {pendientes}.",
            new { pendientes, ultimoHeartbeatUtc = config.UltimoHeartbeatUtc, modo = "pull" });
    }

    public async Task<PorteroSincronizacionDto> SyncAllActiveSociosAsync(int emisorId)
    {
        var config = await GetConfigAsync(emisorId);
        if (config is null || !config.Habilitado)
            return new PorteroSincronizacionDto(0, 0, 0, ["Portero no habilitado para este emisor"]);

        var socios = await db.Socios.Where(s => s.EmisorId == emisorId && s.Estado == EstadoSocio.Activo).ToListAsync();
        foreach (var socio in socios)
            await EnqueueUpsertSocioAsync(socio, config);

        await db.SaveChangesAsync();
        return new PorteroSincronizacionDto(socios.Count, socios.Count, 0, []);
    }

    public async Task TrySyncSocioAsync(Socio socio)
    {
        var config = await GetConfigAsync(socio.EmisorId);
        if (config is null || !config.Habilitado || !config.SincronizarAutomatico) return;
        if (socio.Estado != EstadoSocio.Activo) return;

        await EnqueueUpsertSocioAsync(socio, config);
        await db.SaveChangesAsync();
    }

    public async Task TryRemoveSocioAsync(Socio socio)
    {
        var config = await GetConfigAsync(socio.EmisorId);
        if (config is null || !config.Habilitado || !config.SincronizarAutomatico) return;

        var pin = ToPorteroPin(socio);
        await EnqueueAsync(socio.EmisorId, PorteroComandoTipo.DeleteSocio, $"delete:{pin}", new
        {
            cedula = pin,
            device_sn = config.DeviceSn,
        });
        await db.SaveChangesAsync();
    }

    public async Task ProcessAccessWebhookAsync(string emisorSlug, PorteroWebhookPayload payload)
    {
        var emisor = await db.Emisores.FirstOrDefaultAsync(e => e.Slug == emisorSlug && e.Activo);
        if (emisor is null) throw new InvalidOperationException("Emisor no encontrado");

        if (payload.Event != "access") return;

        var pin = GetString(payload.Data, "pin");
        if (string.IsNullOrEmpty(pin)) return;

        var socio = await FindSocioByPinAsync(emisor.Id, pin);
        if (socio is null)
        {
            logger.LogWarning("Webhook access: socio no encontrado pin={Pin} emisor={Slug}", pin, emisorSlug);
            return;
        }

        var accessType = GetString(payload.Data, "access_type") ?? "entry";
        var tipo = accessType.Equals("exit", StringComparison.OrdinalIgnoreCase) ? TipoIngreso.Salida : TipoIngreso.Entrada;
        var timestamp = GetString(payload.Data, "timestamp");
        var fecha = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(timestamp) && DateTime.TryParse(timestamp, out var parsed))
            fecha = parsed.ToUniversalTime();

        db.Ingresos.Add(new Ingreso
        {
            EmisorId = emisor.Id,
            SocioId = socio.Id,
            FechaHora = fecha,
            Tipo = tipo,
            AccesoPermitido = true,
            MotivoRechazo = $"Portero ({GetString(payload.Data, "method") ?? "biométrico"})",
        });
        await db.SaveChangesAsync();
    }

    public async Task<PorteroAccionDto> AbrirPuertaAsync(int emisorId)
    {
        var config = await GetConfigAsync(emisorId);
        if (config is null || !config.Habilitado)
            throw new InvalidOperationException("Portero no habilitado");

        await EnqueueAsync(emisorId, PorteroComandoTipo.AbrirPuerta, $"abrir:{DateTime.UtcNow.Ticks}", new
        {
            device_sn = config.DeviceSn,
        });
        await db.SaveChangesAsync();
        return new PorteroAccionDto(
            "Comando encolado. El agente en la PC del spa lo tomará en el próximo ciclo (unos segundos).",
            new { modo = "pull" });
    }

    public async Task<PorteroAccionDto> CancelarConsultaUsuariosPorteroAsync(int emisorId)
    {
        var config = await GetConfigAsync(emisorId)
            ?? throw new InvalidOperationException("Portero no configurado");
        if (string.IsNullOrWhiteSpace(config.ApiKey))
            throw new InvalidOperationException("Falta la API Key del portero");

        var sn = string.IsNullOrWhiteSpace(config.DeviceSn) ? "7674222960189" : config.DeviceSn.Trim();
        var baseUrl = ResolveApiPorteroBaseUrl(config.ApiUrl).TrimEnd('/');

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", config.ApiKey.Trim());
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var url = $"{baseUrl}/api/dispositivos/{Uri.EscapeDataString(sn)}/cancelar-consulta-usuarios";
        using var response = await http.PostAsync(url, null);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            var detail = body;
            try
            {
                using var errDoc = JsonDocument.Parse(body);
                if (errDoc.RootElement.TryGetProperty("error", out var errProp))
                    detail = errProp.GetString() ?? detail;
            }
            catch (JsonException) { /* texto */ }

            if (detail.Length > 280) detail = detail[..280];
            throw new InvalidOperationException($"No se pudo cancelar la consulta ({(int)response.StatusCode}). {detail}");
        }

        var anulados = 0;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("comandos_anulados", out var c) && c.TryGetInt32(out var n))
                anulados = n;
        }
        catch (JsonException) { /* ignore */ }

        logger.LogInformation("Consulta USERINFO cancelada emisor={EmisorId} sn={Sn} comandosAnulados={N}", emisorId, sn, anulados);
        return new PorteroAccionDto(
            anulados > 0
                ? $"Consulta de export cancelada ({anulados} QUERY). Socios, sync y abrir puerta no se tocan."
                : "Consulta de export cancelada. Socios, sync y abrir puerta no se tocan.",
            new { device_sn = sn, comandos_anulados = anulados });
    }

    public async Task<List<PorteroAgentComandoDto>> PullComandosAsync(string emisorSlug, string apiKey, int limit = 50)
    {
        var (emisor, config) = await RequireAgentAsync(emisorSlug, apiKey);
        config.UltimoHeartbeatUtc = DateTime.UtcNow;

        // Reclamar pendientes antiguos en Procesando (>5 min) como Pendiente de nuevo
        var stale = DateTime.UtcNow.AddMinutes(-5);
        var stuck = await db.PorteroComandos
            .Where(c => c.EmisorId == emisor.Id && c.Estado == PorteroComandoEstado.Procesando && c.FechaCreacion < stale)
            .ToListAsync();
        foreach (var s in stuck)
            s.Estado = PorteroComandoEstado.Pendiente;

        var items = await db.PorteroComandos
            .Where(c => c.EmisorId == emisor.Id && c.Estado == PorteroComandoEstado.Pendiente)
            .OrderBy(c => c.FechaCreacion)
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync();

        foreach (var item in items)
            item.Estado = PorteroComandoEstado.Procesando;

        await db.SaveChangesAsync();

        return items.Select(c => new PorteroAgentComandoDto(
            c.Id,
            TipoToString(c.Tipo),
            JsonSerializer.Deserialize<object>(c.PayloadJson) ?? new { },
            c.FechaCreacion)).ToList();
    }

    public async Task AckComandoAsync(string emisorSlug, string apiKey, long comandoId, PorteroAgentAckDto ack)
    {
        var (emisor, _) = await RequireAgentAsync(emisorSlug, apiKey);
        var cmd = await db.PorteroComandos.FirstOrDefaultAsync(c => c.Id == comandoId && c.EmisorId == emisor.Id);
        if (cmd is null) throw new InvalidOperationException("Comando no encontrado");

        if (ack.Ok)
        {
            cmd.Estado = PorteroComandoEstado.Hecho;
            cmd.UltimoError = null;
        }
        else
        {
            cmd.Estado = PorteroComandoEstado.Error;
            cmd.UltimoError = ack.Error ?? "Error desconocido";
        }
        cmd.FechaProcesado = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task HeartbeatAsync(string emisorSlug, string apiKey, PorteroAgentHeartbeatDto? dto)
    {
        var (_, config) = await RequireAgentAsync(emisorSlug, apiKey);
        config.UltimoHeartbeatUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        logger.LogDebug("Heartbeat portero emisor={Slug} version={Version}", emisorSlug, dto?.Version);
    }

    private async Task EnqueueUpsertSocioAsync(Socio socio, EmisorPorteroConfig config)
    {
        var pin = ToPorteroPin(socio);
        var nombre = $"{socio.Nombre} {socio.Apellido}".Trim();
        var validDays = socio.FechaVencimiento.HasValue
            ? Math.Max(1, (int)(socio.FechaVencimiento.Value.Date - DateTime.UtcNow.Date).TotalDays)
            : 365;

        await EnqueueAsync(socio.EmisorId, PorteroComandoTipo.UpsertSocio, $"upsert:{pin}", new
        {
            nombre,
            cedula = pin,
            celular = socio.Telefono ?? "",
            email = socio.Email ?? "",
            membership_type = "socio",
            access_level = 1,
            valid_days = validDays,
            device_sn = config.DeviceSn,
        });
    }

    private async Task EnqueueAsync(int emisorId, PorteroComandoTipo tipo, string clave, object payload)
    {
        // Reemplaza pendientes/procesando con la misma clave (última gana)
        var previos = await db.PorteroComandos
            .Where(c => c.EmisorId == emisorId
                        && c.ClaveIdempotencia == clave
                        && (c.Estado == PorteroComandoEstado.Pendiente || c.Estado == PorteroComandoEstado.Procesando))
            .ToListAsync();
        foreach (var p in previos)
            p.Estado = PorteroComandoEstado.Hecho;

        db.PorteroComandos.Add(new PorteroComando
        {
            EmisorId = emisorId,
            Tipo = tipo,
            ClaveIdempotencia = clave,
            PayloadJson = JsonSerializer.Serialize(payload, JsonOpts),
            Estado = PorteroComandoEstado.Pendiente,
            FechaCreacion = DateTime.UtcNow,
        });
    }

    private async Task<(Emisor Emisor, EmisorPorteroConfig Config)> RequireAgentAsync(string emisorSlug, string apiKey)
    {
        var emisor = await db.Emisores
            .Include(e => e.PorteroConfig)
            .FirstOrDefaultAsync(e => e.Slug == emisorSlug && e.Activo)
            ?? throw new UnauthorizedAccessException("Emisor no encontrado");

        var config = emisor.PorteroConfig
            ?? throw new UnauthorizedAccessException("Portero no configurado");

        if (!config.Habilitado || string.IsNullOrWhiteSpace(config.ApiKey) ||
            !string.Equals(config.ApiKey, apiKey, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("API key inválida");

        return (emisor, config);
    }

    private async Task<Socio?> FindSocioByPinAsync(int emisorId, string pin)
    {
        var socios = await db.Socios.Where(s => s.EmisorId == emisorId).ToListAsync();
        return socios.FirstOrDefault(s =>
            ToPorteroPin(s) == pin ||
            new string(s.Cedula.Where(char.IsDigit).ToArray()) == pin ||
            s.NumeroSocio == pin);
    }

    public async Task<(byte[] Content, string FileName)> ExportSociosPorteroCsvAsync(int emisorId, string emisorSlug)
    {
        var config = await GetConfigAsync(emisorId)
            ?? throw new InvalidOperationException("Portero no configurado");
        if (string.IsNullOrWhiteSpace(config.ApiKey))
            throw new InvalidOperationException("Falta la API Key del portero");

        var sn = string.IsNullOrWhiteSpace(config.DeviceSn) ? "7674222960189" : config.DeviceSn.Trim();
        var baseUrl = ResolveApiPorteroBaseUrl(config.ApiUrl).TrimEnd('/');

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(250) };
        http.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", config.ApiKey.Trim());
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // 1) Pedir dump al equipo y esperar a que ApiPorteroSpa acumule
        var exportUrl = $"{baseUrl}/api/dispositivos/{Uri.EscapeDataString(sn)}/exportar-usuarios-equipo?wait_seconds=180&idle_seconds=12";
        using (var exportResp = await http.GetAsync(exportUrl))
        {
            if (!exportResp.IsSuccessStatusCode)
            {
                var detail = await exportResp.Content.ReadAsStringAsync();
                try
                {
                    using var errDoc = JsonDocument.Parse(detail);
                    if (errDoc.RootElement.TryGetProperty("error", out var errProp))
                        detail = errProp.GetString() ?? detail;
                }
                catch (JsonException) { /* texto */ }

                if (detail.Length > 280) detail = detail[..280];
                throw new InvalidOperationException(
                    exportResp.StatusCode == System.Net.HttpStatusCode.GatewayTimeout
                        ? detail
                        : $"No se pudo consultar el equipo ({(int)exportResp.StatusCode}). {detail}");
            }
        }

        // 2) Leer JSON del snapshot del agente
        var listUrl = $"{baseUrl}/api/dispositivos/{Uri.EscapeDataString(sn)}/usuarios-equipo";
        using var listResp = await http.GetAsync(listUrl);
        var listBody = await listResp.Content.ReadAsStringAsync();
        if (!listResp.IsSuccessStatusCode)
            throw new InvalidOperationException($"No se pudo leer usuarios del agente: {(int)listResp.StatusCode}");

        using var doc = JsonDocument.Parse(listBody);
        if (!doc.RootElement.TryGetProperty("usuarios", out var usuariosEl)
            || usuariosEl.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Respuesta inválida de usuarios del equipo");

        var lote = new List<(string Pin, string Nombre, int Privilegio, string Tarjeta)>();
        foreach (var u in usuariosEl.EnumerateArray())
        {
            var pin = GetJsonString(u, "pin").Trim();
            if (string.IsNullOrEmpty(pin)) continue;
            var priRaw = GetJsonString(u, "privilege");
            _ = int.TryParse(priRaw, out var pri);
            lote.Add((pin, GetJsonString(u, "name").Trim(), pri, GetJsonString(u, "card").Trim()));
        }

        // 3) Upsert en GestionSpa (acumula si se corta y se reintenta)
        var ahora = DateTime.UtcNow;
        var existentes = await db.PorteroUsuariosExtraidos
            .Where(x => x.EmisorId == emisorId)
            .ToDictionaryAsync(x => x.Pin, StringComparer.Ordinal);

        var nuevos = 0;
        var actualizados = 0;
        foreach (var (pin, nombre, privilegio, tarjeta) in lote)
        {
            if (existentes.TryGetValue(pin, out var row))
            {
                row.Nombre = nombre;
                row.Privilegio = privilegio;
                row.Tarjeta = tarjeta;
                row.DeviceSn = sn;
                row.UltimaExtraccionUtc = ahora;
                actualizados++;
            }
            else
            {
                var created = new PorteroUsuarioExtraido
                {
                    EmisorId = emisorId,
                    Pin = pin,
                    Nombre = nombre,
                    Privilegio = privilegio,
                    Tarjeta = tarjeta,
                    DeviceSn = sn,
                    PrimeraExtraccionUtc = ahora,
                    UltimaExtraccionUtc = ahora,
                };
                db.PorteroUsuariosExtraidos.Add(created);
                existentes[pin] = created;
                nuevos++;
            }
        }

        await db.SaveChangesAsync();
        logger.LogInformation(
            "PorteroUsuariosExtraidos emisor={EmisorId}: lote={Lote} nuevos={Nuevos} actualizados={Actualizados} total={Total}",
            emisorId, lote.Count, nuevos, actualizados, existentes.Count);

        return await ExportUsuariosExtraidosGuardadosCsvAsync(emisorId, emisorSlug);
    }

    public async Task<(byte[] Content, string FileName)> ExportUsuariosExtraidosGuardadosCsvAsync(int emisorId, string emisorSlug)
    {
        var todos = await db.PorteroUsuariosExtraidos.AsNoTracking()
            .Where(x => x.EmisorId == emisorId)
            .OrderBy(x => x.Pin)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("pin;nombre;privilegio;tarjeta;device_sn;primera_extraccion_utc;ultima_extraccion_utc");
        foreach (var r in todos)
        {
            sb.Append(Csv(r.Pin)).Append(';')
                .Append(Csv(r.Nombre)).Append(';')
                .Append(r.Privilegio).Append(';')
                .Append(Csv(r.Tarjeta)).Append(';')
                .Append(Csv(r.DeviceSn)).Append(';')
                .Append(Csv(r.PrimeraExtraccionUtc.ToString("o"))).Append(';')
                .Append(Csv(r.UltimaExtraccionUtc.ToString("o")))
                .AppendLine();
        }

        var fileName = $"usuarios-equipo-{emisorSlug}-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv";
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return (bytes, fileName);
    }

    private static string ResolveApiPorteroBaseUrl(string? apiUrl)
    {
        var env = Environment.GetEnvironmentVariable("API_PORTERO_BASE_URL");
        if (!string.IsNullOrWhiteSpace(env))
            return env.Trim().TrimEnd('/');

        if (!string.IsNullOrWhiteSpace(apiUrl)
            && !string.Equals(apiUrl.Trim(), "pull", StringComparison.OrdinalIgnoreCase)
            && (apiUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || apiUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            return apiUrl.Trim().TrimEnd('/');

        return "https://apiporterospa-production.up.railway.app";
    }

    private static string GetJsonString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind != JsonValueKind.Null ? (p.ToString() ?? "") : "";

    private static string Csv(string? value)
    {
        var v = value ?? "";
        if (v.Contains('"') || v.Contains(';') || v.Contains('\n') || v.Contains('\r'))
            return $"\"{v.Replace("\"", "\"\"")}\"";
        return v;
    }

    public static string ToPorteroPin(Socio socio)
    {
        var digits = new string(socio.Cedula.Where(char.IsDigit).ToArray());
        if (digits.Length > 0)
            return digits.Length > 9 ? digits[^9..] : digits;
        return socio.NumeroSocio;
    }

    private static string TipoToString(PorteroComandoTipo tipo) => tipo switch
    {
        PorteroComandoTipo.UpsertSocio => "upsert_socio",
        PorteroComandoTipo.DeleteSocio => "delete_socio",
        PorteroComandoTipo.AbrirPuerta => "abrir_puerta",
        _ => tipo.ToString().ToLowerInvariant(),
    };

    private static string? GetString(JsonElement data, string name) =>
        data.TryGetProperty(name, out var prop) ? prop.GetString() : null;

    private static PorteroConfigDto MapConfigDto(EmisorPorteroConfig? config, string emisorSlug, string? apiPublicBaseUrl, int pendientes)
    {
        var baseUrl = (apiPublicBaseUrl ?? "").TrimEnd('/');
        var webhookUrl = string.IsNullOrEmpty(baseUrl)
            ? $"/api/webhooks/portero/{emisorSlug}"
            : $"{baseUrl}/api/webhooks/portero/{emisorSlug}";
        var agentPullUrl = string.IsNullOrEmpty(baseUrl)
            ? $"/api/portero/agent/{emisorSlug}"
            : $"{baseUrl}/api/portero/agent/{emisorSlug}";

        if (config is null)
            return new PorteroConfigDto(false, "pull", "", null, "7674222960189", true, webhookUrl, null, agentPullUrl, null, 0);

        return new PorteroConfigDto(
            config.Habilitado,
            config.ApiUrl,
            config.ApiKey,
            config.WebhookSecret,
            config.DeviceSn,
            config.SincronizarAutomatico,
            webhookUrl,
            config.FechaActualizacion,
            agentPullUrl,
            config.UltimoHeartbeatUtc,
            pendientes);
    }
}
