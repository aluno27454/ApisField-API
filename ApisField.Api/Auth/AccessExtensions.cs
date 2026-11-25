using System.Security.Claims;
using ApisField.Api.Data;

namespace ApisField.Api.Auth;

/// <summary>Filtros de acesso: limitam uma consulta ao que o utilizador pode ver.</summary>
/// <remarks>
/// Ficam num só sítio para nenhuma rota se esquecer deles. Como o que é de outro utilizador nem sequer sai
/// da consulta, a resposta é um 404 igual ao de um registo que não existe (contrato).
/// </remarks>
public static class AccessExtensions
{
    /// <summary>As colmeias do utilizador, ou todas se for admin.</summary>
    public static IQueryable<Hive> VisibleTo(this IQueryable<Hive> hives, ClaimsPrincipal principal)
    {
        if (principal.IsAdmin()) return hives;
        var userId = principal.UserId();
        return hives.Where(h => h.OwnerId == userId);
    }

    /// <summary>As visitas às colmeias do utilizador, ou todas se for admin.</summary>
    public static IQueryable<Visit> VisibleTo(this IQueryable<Visit> visits, ClaimsPrincipal principal)
    {
        if (principal.IsAdmin()) return visits;
        var userId = principal.UserId();
        return visits.Where(v => v.Hive!.OwnerId == userId);
    }

    /// <summary>As fotografias das visitas às colmeias do utilizador, ou todas se for admin.</summary>
    public static IQueryable<Photo> VisibleTo(this IQueryable<Photo> photos, ClaimsPrincipal principal)
    {
        if (principal.IsAdmin()) return photos;
        var userId = principal.UserId();
        return photos.Where(p => p.Visit!.Hive!.OwnerId == userId);
    }
}
