using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ApisField.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ApisField.Api.Tests;

/// <summary>Arranca a API em memória, para testes de integração, com uma base de dados SQLite própria.</summary>
/// <remarks>
/// Cada instância usa um ficheiro temporário diferente, por isso os testes não partilham dados
/// e podem correr em paralelo. O ficheiro é apagado no <see cref="Dispose(bool)"/>.
/// Usar sempre com <c>using</c>.
/// </remarks>
/// <param name="dbPath">
/// Ficheiro da base de dados a usar. Por omissão, um ficheiro novo na pasta temporária.
/// Passar um caminho existente simula um reinício da API sobre os mesmos dados.
/// </param>
public class ApiFactory(string? dbPath = null) : WebApplicationFactory<Program>
{
    /// <summary>Caminho do ficheiro SQLite usado por esta instância.</summary>
    public string DbPath { get; } = dbPath ?? Path.Combine(Path.GetTempPath(), $"apisfield-test-{Guid.NewGuid():N}.db");

    /// <summary>
    /// Se <c>true</c> (por omissão), apaga o ficheiro no fim. Pôr a <c>false</c> para o reutilizar
    /// noutra instância, como no teste de reinício.
    /// </summary>
    public bool DeleteDbOnDispose { get; init; } = true;

    /// <summary>Pasta temporária onde esta instância guarda as fotografias.</summary>
    public string PhotosPath { get; } = Path.Combine(Path.GetTempPath(), $"apisfield-fotos-{Guid.NewGuid():N}");

    /// <summary>Cliente HTTP com a sessão iniciada na conta indicada (ver README).</summary>
    public async Task<HttpClient> ClientAsync(string email, string password = "Abelha2026!")
    {
        var client = CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        res.EnsureSuccessStatusCode();
        var token = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Sessão do André, dono de 4 colmeias.</summary>
    public Task<HttpClient> AndreAsync() => ClientAsync("andre@apisfield.pt");

    /// <summary>Sessão do Samuel, dono de 2 colmeias.</summary>
    public Task<HttpClient> SamuelAsync() => ClientAsync("samuel@apisfield.pt");

    /// <summary>Sessão do administrador.</summary>
    public Task<HttpClient> AdminAsync() => ClientAsync("admin@apisfield.pt", "Admin2026!");

    /// <summary>Lê da base de dados, sem passar pela API.</summary>
    public T Query<T>(Func<AppDbContext, T> query)
    {
        using var scope = Services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Photos:Path", PhotosPath);
        // Pooling=False fecha a ligação ao fim de cada uso. Com pooling, o Windows mantém o ficheiro
        // bloqueado e o File.Delete do Dispose falha.
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={DbPath};Pooling=False");
    }

    protected override void Dispose(bool disposing)
    {
        // Primeiro parar a API (e fechar as ligações à base de dados), só depois apagar o ficheiro.
        base.Dispose(disposing);
        if (!disposing) return;
        if (DeleteDbOnDispose) File.Delete(DbPath);
        if (Directory.Exists(PhotosPath)) Directory.Delete(PhotosPath, recursive: true);
    }
}
