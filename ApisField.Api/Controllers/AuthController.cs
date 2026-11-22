using ApisField.Api.Auth;
using ApisField.Api.Data;
using ApisField.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApisField.Api.Controllers;

/// <summary>Registo e login. As duas rotas são públicas: é aqui que a app obtém o token.</summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController(AppDbContext db, TokenService tokens) : ControllerBase
{
    /// <summary>POST /api/auth/register: cria a conta e já devolve o token, para a app não ter de fazer login a seguir.</summary>
    /// <returns>201 com <see cref="AuthResult"/>; 400 se os dados forem inválidos; 409 se o email já existir.</returns>
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        var email = Normalize(req.Email);
        if (await db.Users.AnyAsync(u => u.Email == email))
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Este email já está registado.");

        var user = new User { Name = req.Name.Trim(), Email = email, PasswordHash = PasswordHasher.Hash(req.Password) };
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Dois registos com o mesmo email ao mesmo tempo: o índice único da base de dados trava o segundo.
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Este email já está registado.");
        }

        return Created("/api/me", new AuthResult(tokens.Create(user), UserResponse.From(user)));
    }

    /// <summary>POST /api/auth/login</summary>
    /// <returns>200 com <see cref="AuthResult"/>; 401 se o email ou a palavra-passe estiverem errados.</returns>
    /// <remarks>
    /// A resposta é igual para um email que não existe e para uma palavra-passe errada,
    /// para não revelar que emails estão registados.
    /// </remarks>
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var email = Normalize(req.Email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email);
        if (user is null || !PasswordHasher.Verify(req.Password, user.PasswordHash))
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Email ou palavra-passe errados.");

        return Ok(new AuthResult(tokens.Create(user), UserResponse.From(user)));
    }

    /// <summary>Os emails guardam-se sem espaços e em minúsculas, para "Andre@..." e "andre@..." serem a mesma conta.</summary>
    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
