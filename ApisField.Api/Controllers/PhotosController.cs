using ApisField.Api.Auth;
using ApisField.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApisField.Api.Controllers;

/// <summary>Leitura e remoção de fotografias. O envio faz-se na visita (<see cref="VisitsController.AddPhoto"/>).</summary>
[ApiController]
[Route("api/photos/{id:int}")]
public class PhotosController(AppDbContext db, PhotoStore store) : ControllerBase
{
    /// <summary>GET /api/photos/{id}</summary>
    /// <returns>200 com a imagem; 404 se não existir ou for de outro utilizador.</returns>
    [HttpGet]
    public async Task<IActionResult> Get(int id)
    {
        var photo = await db.Photos.VisibleTo(User).SingleOrDefaultAsync(p => p.Id == id);
        var path = photo is null ? null : store.Find(photo.FileName);
        return path is null ? NotFound() : PhysicalFile(path, "image/jpeg");
    }

    /// <summary>DELETE /api/photos/{id}</summary>
    [HttpDelete]
    public async Task<IActionResult> Delete(int id)
    {
        var photo = await db.Photos.VisibleTo(User).SingleOrDefaultAsync(p => p.Id == id);
        if (photo is null) return NotFound();

        db.Photos.Remove(photo);
        await db.SaveChangesAsync();
        store.Delete([photo.FileName]);
        return NoContent();
    }
}
