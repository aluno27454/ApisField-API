using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApisField.Api.Controllers;

/// <summary>Verificação de disponibilidade da API. Não exige autenticação.</summary>
/// <remarks>
/// Usada pela app para saber se o servidor responde; serve também de health check no Azure App Service.
/// Não toca na base de dados, para responder depressa e não falhar por causa de um problema nela.
/// </remarks>
[ApiController]
[Route("api/health")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    /// <summary>GET /api/health</summary>
    /// <returns>200 com <c>{ "status": "ok" }</c>.</returns>
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok" });
}
