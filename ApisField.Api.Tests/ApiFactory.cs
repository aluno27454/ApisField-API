using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

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

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        // Pooling=False fecha a ligação ao fim de cada uso. Com pooling, o Windows mantém o ficheiro
        // bloqueado e o File.Delete do Dispose falha.
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={DbPath};Pooling=False");

    protected override void Dispose(bool disposing)
    {
        // Primeiro parar a API (e fechar as ligações à base de dados), só depois apagar o ficheiro.
        base.Dispose(disposing);
        if (disposing && DeleteDbOnDispose) File.Delete(DbPath);
    }
}
