using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ApisField.Api.Data;

/// <summary>Contexto EF Core da API, sobre SQLite.</summary>
/// <remarks>
/// O esquema é criado com <c>EnsureCreated</c> no arranque (ver <c>Program.cs</c>), sem migrações:
/// a base de dados é descartável e volta a ser preenchida pelo <see cref="Seed"/>.
/// Se um dia for preciso preservar dados entre versões do esquema, passar para migrações.
/// </remarks>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Hive> Hives => Set<Hive>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<WaspSighting> Sightings => Set<WaspSighting>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        // O SQLite não guarda o fuso horário: sem isto, as datas lidas vêm como Unspecified e o JSON sai sem "Z".
        // Todas as datas da API são UTC (contrato), por isso convertem-se ao gravar e marcam-se como UTC ao ler.
        b.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    protected override void OnModelCreating(ModelBuilder b)
    {
        // O registo devolve 409 para emails repetidos; o índice garante-o mesmo com pedidos em simultâneo.
        b.Entity<User>().HasIndex(u => u.Email).IsUnique();

        // DELETE /api/hives/{id} apaga também as visitas e os alertas dessa colmeia (contrato).
        b.Entity<Visit>().HasOne(v => v.Hive).WithMany().OnDelete(DeleteBehavior.Cascade);
        b.Entity<Alert>().HasOne(a => a.Hive).WithMany().OnDelete(DeleteBehavior.Cascade);

        // Os registos das fotografias desaparecem com a visita. Os ficheiros apagam-se à parte (PhotoStore).
        b.Entity<Photo>().HasOne(p => p.Visit).WithMany(v => v.Photos).OnDelete(DeleteBehavior.Cascade);
    }
}
