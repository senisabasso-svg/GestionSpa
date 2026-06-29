using GestionSpa.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace GestionSpa.Api.Services;

public static class TenantExtensions
{
    public static int RequireEmisorId(this ITenantContext tenant)
    {
        if (!tenant.EffectiveEmisorId.HasValue)
            throw new TenantException("Debés seleccionar un emisor para esta operación");
        return tenant.EffectiveEmisorId.Value;
    }

    public static IQueryable<T> ForTenant<T>(this IQueryable<T> query, ITenantContext tenant) where T : class, IEmisorEntity
    {
        var emisorId = tenant.RequireEmisorId();
        return query.Where(e => e.EmisorId == emisorId);
    }

    public static void SetEmisorId<T>(this T entity, ITenantContext tenant) where T : class, IEmisorEntity
    {
        entity.EmisorId = tenant.RequireEmisorId();
    }

    public static ActionResult? ValidateSameEmisor(this ITenantContext tenant, IEmisorEntity entity)
    {
        if (!tenant.EffectiveEmisorId.HasValue || entity.EmisorId != tenant.EffectiveEmisorId)
            return new NotFoundResult();
        return null;
    }
}

public class TenantException(string message) : Exception(message);
