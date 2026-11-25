using ApisField.Api.Auth;
using ApisField.Api.Data;
using ApisField.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApisField.Api.Controllers;

/// <summary>Alertas das colmeias do utilizador (do admin, de todas). Os alheios dão 404 (contrato).</summary>
[ApiController]
[Route("api/alerts")]
public class AlertsController(AppDbContext db) : ControllerBase
{
    /// <summary>GET /api/alerts. Antes de responder, gera os alertas das condições presentes agora.</summary>
    /// <returns>200 com os alertas, do mais recente para o mais antigo.</returns>
    [HttpGet]
    public async Task<IActionResult> List()
    {
        await AlertMonitor.RefreshAsync(db, db.Hives.VisibleTo(User), DateTime.UtcNow);

        // O id desempata alertas criados no mesmo instante, para a ordem ser sempre a mesma.
        var alerts = await db.Alerts.VisibleTo(User).Include(a => a.Hive)
            .OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id).ToListAsync();
        return Ok(alerts.Select(AlertResponse.From));
    }

    /// <summary>PATCH /api/alerts/{id}/read</summary>
    /// <returns>200 com o alerta marcado como lido.</returns>
    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var alert = await db.Alerts.VisibleTo(User).Include(a => a.Hive).SingleOrDefaultAsync(a => a.Id == id);
        if (alert is null) return NotFound();

        alert.Read = true;
        await db.SaveChangesAsync();
        return Ok(AlertResponse.From(alert));
    }

    /// <summary>PATCH /api/alerts/read-all: marca como lidos todos os alertas do utilizador.</summary>
    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await db.Alerts.VisibleTo(User).Where(a => !a.Read).ExecuteUpdateAsync(s => s.SetProperty(a => a.Read, true));
        return NoContent();
    }
}
