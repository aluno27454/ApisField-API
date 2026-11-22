using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ApisField.Api.Auth;

/// <summary>Leitura dos dados do utilizador autenticado a partir dos claims do token.</summary>
public static class ClaimsExtensions
{
    /// <summary>Id do utilizador que fez o pedido (claim <c>sub</c>).</summary>
    /// <remarks>Só se usa em rotas autenticadas, onde o token já foi validado e o <c>sub</c> existe.</remarks>
    public static int UserId(this ClaimsPrincipal principal) =>
        int.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    /// <summary>Indica se o utilizador tem o papel <c>admin</c>.</summary>
    public static bool IsAdmin(this ClaimsPrincipal principal) => principal.IsInRole("admin");
}
