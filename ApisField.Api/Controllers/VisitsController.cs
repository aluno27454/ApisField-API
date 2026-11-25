using ApisField.Api.Auth;
using ApisField.Api.Data;
using ApisField.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApisField.Api.Controllers;

/// <summary>Visitas às colmeias. Cada utilizador só vê e altera as visitas às suas colmeias; o admin, todas.</summary>
[ApiController]
[Route("api")]
public class VisitsController(AppDbContext db, PhotoStore store) : ControllerBase
{
    /// <summary>Fotografias por visita, no máximo (contrato).</summary>
    private const int MaxPhotos = 5;

    /// <summary>As visitas que o utilizador pode ver, com o que a resposta precisa.</summary>
    private IQueryable<Visit> Visits => db.Visits.VisibleTo(User).Include(v => v.Hive).Include(v => v.Photos);

    /// <summary>GET /api/visits?hiveId=</summary>
    /// <returns>200 com as visitas, da mais recente para a mais antiga; 404 se a colmeia indicada não existir ou não for do utilizador.</returns>
    [HttpGet("visits")]
    public async Task<IActionResult> List(int? hiveId)
    {
        var query = Visits;
        if (hiveId is not null)
        {
            if (!await db.Hives.VisibleTo(User).AnyAsync(h => h.Id == hiveId)) return NotFound();
            query = query.Where(v => v.HiveId == hiveId);
        }

        // O id desempata visitas com a mesma data, para a ordem ser sempre a mesma.
        var visits = await query.OrderByDescending(v => v.Date).ThenByDescending(v => v.Id).ToListAsync();
        return Ok(visits.Select(VisitResponse.From));
    }

    /// <summary>GET /api/visits/{id}</summary>
    [HttpGet("visits/{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var visit = await Visits.SingleOrDefaultAsync(v => v.Id == id);
        return visit is null ? NotFound() : Ok(VisitResponse.From(visit));
    }

    /// <summary>POST /api/hives/{hiveId}/visits</summary>
    /// <returns>201 com a visita; 400 se os dados forem inválidos; 404 se a colmeia não for do utilizador.</returns>
    [HttpPost("hives/{hiveId:int}/visits")]
    public async Task<IActionResult> Create(int hiveId, VisitRequest req)
    {
        var hive = await db.Hives.VisibleTo(User).SingleOrDefaultAsync(h => h.Id == hiveId);
        if (hive is null) return NotFound();

        // BroodLevel e Temperament são "required" na entidade; o ApplyTo preenche tudo o resto.
        var visit = new Visit { Hive = hive, AuthorId = User.UserId(), BroodLevel = req.BroodLevel!, Temperament = req.Temperament! };
        req.ApplyTo(visit);
        db.Visits.Add(visit);
        await db.SaveChangesAsync();
        return Created($"/api/visits/{visit.Id}", VisitResponse.From(visit));
    }

    /// <summary>PUT /api/visits/{id}. A colmeia e o autor não mudam.</summary>
    [HttpPut("visits/{id:int}")]
    public async Task<IActionResult> Update(int id, VisitRequest req)
    {
        var visit = await Visits.SingleOrDefaultAsync(v => v.Id == id);
        if (visit is null) return NotFound();

        req.ApplyTo(visit);
        await db.SaveChangesAsync();
        return Ok(VisitResponse.From(visit));
    }

    /// <summary>DELETE /api/visits/{id}. Apaga também as fotografias.</summary>
    [HttpDelete("visits/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var visit = await Visits.SingleOrDefaultAsync(v => v.Id == id);
        if (visit is null) return NotFound();

        var files = visit.Photos.Select(p => p.FileName).ToList();
        db.Visits.Remove(visit);
        await db.SaveChangesAsync();
        store.Delete(files);
        return NoContent();
    }

    /// <summary>POST /api/visits/{id}/photos, com a imagem no corpo do pedido.</summary>
    /// <returns>201 com o id da fotografia; 400 se não for JPEG, passar de 2 MB ou a visita já tiver 5.</returns>
    [HttpPost("visits/{id:int}/photos")]
    public async Task<IActionResult> AddPhoto(int id)
    {
        if (!await db.Visits.VisibleTo(User).AnyAsync(v => v.Id == id)) return NotFound();

        var (jpeg, error) = await JpegReader.ReadAsync(Request);
        if (jpeg is null) return InvalidPhoto(error!);

        // Em SQLite, a transação bloqueia logo as outras escritas. Assim, dois envios ao mesmo tempo contam
        // as fotografias um de cada vez e a visita nunca passa do limite.
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (await db.Photos.CountAsync(p => p.VisitId == id) >= MaxPhotos)
            return InvalidPhoto($"Cada visita tem no máximo {MaxPhotos} fotografias.");

        var photo = new Photo { VisitId = id, FileName = await store.SaveAsync(jpeg) };
        try
        {
            db.Photos.Add(photo);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            // Sem o registo, ninguém voltaria a encontrar este ficheiro para o apagar.
            store.Delete([photo.FileName]);
            throw;
        }
        return Created($"/api/photos/{photo.Id}", new { id = photo.Id });
    }

    private IActionResult InvalidPhoto(string message)
    {
        ModelState.AddModelError("photo", message);
        return ValidationProblem(ModelState);
    }
}
