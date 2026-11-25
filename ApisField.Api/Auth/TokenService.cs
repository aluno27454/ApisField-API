using System.Security.Claims;
using System.Text;
using ApisField.Api.Data;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ApisField.Api.Auth;

/// <summary>Cria os tokens JWT entregues no registo e no login.</summary>
/// <remarks>
/// O token é assinado com HMAC-SHA256 (HS256) com a chave <c>Jwt:Key</c>. Quem não tiver a chave
/// não consegue criar nem alterar um token válido: qualquer alteração invalida a assinatura e a API
/// responde 401. A validação é configurada em <c>Program.cs</c> com os mesmos valores deste ficheiro.
/// </remarks>
public class TokenService(SymmetricSecurityKey key)
{
    /// <summary>Quem emite o token (validado em cada pedido).</summary>
    public const string Issuer = "ApisField.Api";

    /// <summary>A quem se destina o token (validado em cada pedido).</summary>
    public const string Audience = "ApisField.App";

    /// <summary>Validade do token, definida no contrato. Depois disto a app volta ao login.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    /// <summary>Cria a chave de assinatura a partir do texto de <c>Jwt:Key</c>.</summary>
    /// <exception cref="InvalidOperationException">Se a chave faltar ou tiver menos de 32 bytes (o mínimo para HS256).</exception>
    public static SymmetricSecurityKey KeyFrom(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || Encoding.UTF8.GetByteCount(text) < 32)
            throw new InvalidOperationException(
                "Falta a chave Jwt:Key (pelo menos 32 caracteres). Em produção define-se na variável de ambiente Jwt__Key.");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(text));
    }

    /// <summary>Gera um token para <paramref name="user"/>, válido durante <see cref="Lifetime"/>.</summary>
    /// <remarks>
    /// Os claims identificam o utilizador (<c>sub</c>) e o papel (<c>role</c>), para os controllers
    /// saberem quem faz o pedido e se é administrador sem irem à base de dados.
    /// </remarks>
    public string Create(User user)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Expires = DateTime.UtcNow.Add(Lifetime),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("role", user.Role),
            ]),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
