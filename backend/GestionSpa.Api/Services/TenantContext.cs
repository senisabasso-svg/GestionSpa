using System.Security.Claims;
using GestionSpa.Api.Models;
using Microsoft.AspNetCore.Http;

namespace GestionSpa.Api.Services;

public class TenantContext(IHttpContextAccessor http) : ITenantContext
{
    public const string EmisorHeader = "X-Emisor-Id";

    public int? UserId => int.TryParse(http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public string? Email => http.HttpContext?.User.FindFirstValue(ClaimTypes.Email);
    public RolUsuario? Rol => Enum.TryParse<RolUsuario>(http.HttpContext?.User.FindFirstValue(ClaimTypes.Role), out var r) ? r : null;
    public int? EmisorId => int.TryParse(http.HttpContext?.User.FindFirstValue("emisorId"), out var id) ? id : null;

    public bool IsSuperAdmin => Rol == RolUsuario.SuperAdmin;

    public int? EffectiveEmisorId
    {
        get
        {
            if (EmisorId.HasValue) return EmisorId;
            if (!IsSuperAdmin) return null;
            var header = http.HttpContext?.Request.Headers[EmisorHeader].FirstOrDefault();
            return int.TryParse(header, out var id) ? id : null;
        }
    }
}
