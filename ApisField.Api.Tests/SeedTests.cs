using ApisField.Api.Data;
using Microsoft.Extensions.DependencyInjection;

namespace ApisField.Api.Tests;

/// <summary>Testes dos dados iniciais criados no arranque da API.</summary>
public class SeedTests
{
    /// <summary>Conta os registos de cada tabela. Aceder a <c>factory.Services</c> arranca a API e, com ela, o seed.</summary>
    private static (int users, int hives, int visits, int sightings) Counts(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (db.Users.Count(), db.Hives.Count(), db.Visits.Count(), db.Sightings.Count());
    }

    [Fact]
    public void Seed_CriaDadosIniciais()
    {
        using var factory = new ApiFactory();

        var (users, hives, visits, sightings) = Counts(factory);

        Assert.Equal(3, users);
        Assert.Equal(6, hives);
        Assert.True(visits > 0);
        Assert.True(sightings > 0);
    }

    [Fact]
    public void Seed_NaoDuplicaAoReiniciar()
    {
        // Arranca e para a API mantendo o ficheiro; depois volta a arrancá-la sobre o mesmo ficheiro.
        string path;
        (int, int, int, int) first;
        using (var factory = new ApiFactory { DeleteDbOnDispose = false })
        {
            path = factory.DbPath;
            first = Counts(factory);
        }

        using var restarted = new ApiFactory(path);
        Assert.Equal(first, Counts(restarted));
    }

    [Fact]
    public void Seed_PalavrasPasseGuardadasComHash()
    {
        using var factory = new ApiFactory();
        using var scope = factory.Services.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Single(u => u.Email == "admin@apisfield.pt");

        Assert.Equal("admin", admin.Role);
        Assert.NotEqual("Admin2026!", admin.PasswordHash);
        Assert.True(PasswordHasher.Verify("Admin2026!", admin.PasswordHash));
        Assert.False(PasswordHasher.Verify("errada", admin.PasswordHash));
    }
}
