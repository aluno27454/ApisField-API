using ApisField.Api.Auth;
using ApisField.Api.Data;
using ApisField.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApisField.Api.Controllers;

/// <summary>Colmeias. Cada utilizador só vê e altera as suas; o admin, todas. As alheias dão 404 (contrato).</summary>
[ApiController]
[Route("api/hives")]
public class HivesController(AppDbContext db, PhotoStore store) : ControllerBase
{
    /// <summary>As colmeias que o utilizador pode ver.</summary>
    private IQueryable<Hive> Hives => db.Hives.VisibleTo(User);

    /// <summary>GET /api/hives</summary>
    /// <returns>200 com as colmeias do utilizador (todas, para o admin), por ordem de registo.</returns>
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var hives = await Hives.OrderBy(h => h.Id).ToListAsync();
        var now = DateTime.UtcNow;
        return Ok(hives.Select(h => HiveResponse.From(h, now)));
    }

    /// <summary>GET /api/hives/{id}</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var hive = await Hives.SingleOrDefaultAsync(h => h.Id == id);
        return hive is null ? NotFound() : Ok(HiveResponse.From(hive, DateTime.UtcNow));
    }

    /// <summary>GET /api/hives/{id}/history</summary>
    /// <returns>200 com a temperatura interior, o peso e (só no Pro) o CO₂ dos últimos 7 dias.</returns>
    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> History(int id)
    {
        var hive = await Hives.SingleOrDefaultAsync(h => h.Id == id);
        return hive is null ? NotFound() : Ok(HiveHistory.From(hive, DateTime.UtcNow));
    }

    /// <summary>POST /api/hives. A colmeia fica do utilizador que a cria.</summary>
    /// <returns>201 com a colmeia; 400 se os dados forem inválidos.</returns>
    [HttpPost]
    public async Task<IActionResult> Create(HiveRequest req)
    {
        var now = DateTime.UtcNow;
        // Name é "required" na entidade; o ApplyTo preenche tudo o resto. Os sensores ficam com os valores
        // de referência de uma colmeia nova (ver Hive).
        var hive = new Hive { Name = req.Name!, OwnerId = User.UserId(), CreatedAt = now };
        req.ApplyTo(hive);
        db.Hives.Add(hive);
        await db.SaveChangesAsync();
        return Created($"/api/hives/{hive.Id}", HiveResponse.From(hive, now));
    }

    /// <summary>PUT /api/hives/{id}. O dono não muda.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, HiveRequest req)
    {
        var hive = await Hives.SingleOrDefaultAsync(h => h.Id == id);
        if (hive is null) return NotFound();

        req.ApplyTo(hive);
        await db.SaveChangesAsync();
        return Ok(HiveResponse.From(hive, DateTime.UtcNow));
    }

    /// <summary>DELETE /api/hives/{id}. Apaga também as visitas, as fotografias e os alertas (contrato).</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var hive = await Hives.SingleOrDefaultAsync(h => h.Id == id);
        if (hive is null) return NotFound();

        // Os registos desaparecem em cascata na base de dados, mas os ficheiros não: guardam-se os nomes antes.
        var files = await db.Photos.Where(p => p.Visit!.HiveId == id).Select(p => p.FileName).ToListAsync();
        db.Hives.Remove(hive);
        await db.SaveChangesAsync();
        store.Delete(files);
        return NoContent();
    }
}
