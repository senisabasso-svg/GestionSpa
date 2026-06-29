using GestionSpa.Api.Models;

namespace GestionSpa.Api.Services;

public interface ITenantContext
{
    int? UserId { get; }
    string? Email { get; }
    RolUsuario? Rol { get; }
    int? EmisorId { get; }
    int? EffectiveEmisorId { get; }
    bool IsSuperAdmin { get; }
    bool HasTenant => EffectiveEmisorId.HasValue;
}
