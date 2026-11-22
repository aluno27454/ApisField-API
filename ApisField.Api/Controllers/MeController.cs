using ApisField.Api.Auth;
using ApisField.Api.Data;
using ApisField.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace ApisField.Api.Controllers;

/// <summary>Dados do utilizador da sessão atual. Exige token (política por omissão em <c>Program.cs</c>).</summary>
[ApiController]
[Route("api/me")]
public class MeController(AppDbContext db) : ControllerBase
{
    /// <summary>GET /api/me</summary>
    /// <returns>200 com o utilizador; 401 se o token for de uma conta que já não existe.</returns>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var user = await db.Users.FindAsync(User.UserId());
        return user is null ? Unauthorized() : Ok(UserResponse.From(user));
    }
}
