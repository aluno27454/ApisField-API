using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;

namespace ApisField.Api.Tests;

/// <summary>Testes dos alertas: geração pelos limiares, sem duplicar, acesso e marcação como lidos.</summary>
public class AlertTests
{
    private record AlertDto(int Id, int HiveId, string HiveName, string Type, string Severity, string Message,
        DateTime Timestamp, bool Read);

    private static Task<AlertDto[]> AlertasAsync(HttpClient client) =>
        client.GetFromJsonAsync<AlertDto[]>("/api/alerts")!;

    /// <summary>Muda o valor de referência da bateria de uma colmeia, para provocar ou acabar com um alerta.</summary>
    private static void Bateria(ApiFactory f, string hive, int valor) =>
        f.Query(db => db.Hives.Where(h => h.Name == hive).ExecuteUpdate(s => s.SetProperty(h => h.Battery, valor)));

    [Fact]
    public async Task Lista_GeraOsAlertasDosDadosIniciais_SemDuplicar()
    {
        using var f = new ApiFactory();
        var andre = await f.AndreAsync();

        var primeira = await AlertasAsync(andre);
        var segunda = await AlertasAsync(andre);

        Assert.Equal(
            [("Alecrim", "co2", "warning"), ("Olival de Paialvo", "battery", "warning"), ("Sobreiro Velho", "wasp", "danger")],
            primeira.Select(a => (a.HiveName, a.Type, a.Severity)).Order());
        Assert.All(primeira, a => Assert.False(a.Read));
        Assert.Contains(primeira, a => a.Message == "Bateria a 15 %, abaixo de 20 %.");
        Assert.Equal(primeira.Select(a => a.Id), segunda.Select(a => a.Id));
    }

    [Fact]
    public async Task Lista_SoTrazOsAlertasDasMinhasColmeias()
    {
        using var f = new ApiFactory();

        Assert.Equal(3, (await AlertasAsync(await f.AndreAsync())).Length);
        Assert.Empty(await AlertasAsync(await f.SamuelAsync())); // as do Samuel estão saudáveis
        Assert.Equal(3, (await AlertasAsync(await f.AdminAsync())).Length);
    }

    [Fact]
    public async Task PedidosEmSimultaneo_NaoDuplicamAlertas()
    {
        using var f = new ApiFactory();
        var andre = await f.AndreAsync();

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => andre.GetAsync("/api/alerts")));

        Assert.Equal(3, f.Query(db => db.Alerts.Count()));
    }

    [Fact]
    public async Task CondicaoQuePassaEVolta_GeraUmAlertaNovo_EOMaisRecenteVemPrimeiro()
    {
        using var f = new ApiFactory();
        var andre = await f.AndreAsync();
        await AlertasAsync(andre);

        Bateria(f, "Olival de Paialvo", 90);
        Assert.Single(await AlertasAsync(andre), a => a.Type == "battery"); // o antigo continua na lista
        Bateria(f, "Olival de Paialvo", 10);
        var alertas = await AlertasAsync(andre);

        var bateria = alertas.Where(a => a.Type == "battery").ToArray();
        Assert.Equal(2, bateria.Length);
        Assert.Equal("Bateria a 10 %, abaixo de 20 %.", alertas[0].Message);
        Assert.Equal(alertas.OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id), alertas);
    }

    [Fact]
    public async Task MarcarComoLido_Devolve200ComOAlertaLido()
    {
        using var f = new ApiFactory();
        var andre = await f.AndreAsync();
        var alerta = (await AlertasAsync(andre))[0];

        var res = await andre.PatchAsync($"/api/alerts/{alerta.Id}/read", null);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.True((await res.Content.ReadFromJsonAsync<AlertDto>())!.Read);
        Assert.True((await AlertasAsync(andre)).Single(a => a.Id == alerta.Id).Read);
    }

    [Fact]
    public async Task AlertaDeOutroUtilizador_Devolve404_ENaoMudaNada()
    {
        using var f = new ApiFactory();
        var alerta = (await AlertasAsync(await f.AndreAsync()))[0];

        var res = await (await f.SamuelAsync()).PatchAsync($"/api/alerts/{alerta.Id}/read", null);

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.False(f.Query(db => db.Alerts.Single(a => a.Id == alerta.Id).Read));
    }

    [Fact]
    public async Task MarcarTodosComoLidos_SoMarcaOsMeus()
    {
        using var f = new ApiFactory();
        var andre = await f.AndreAsync();
        var samuel = await f.SamuelAsync();
        Bateria(f, "Carvalhal", 5); // do Samuel
        await AlertasAsync(andre);
        await AlertasAsync(samuel);

        var res = await andre.PatchAsync("/api/alerts/read-all", null);

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.All(await AlertasAsync(andre), a => Assert.True(a.Read));
        Assert.All(await AlertasAsync(samuel), a => Assert.False(a.Read));
    }

    [Fact]
    public async Task SemToken_Devolve401()
    {
        using var f = new ApiFactory();
        var client = f.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/alerts")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PatchAsync("/api/alerts/read-all", null)).StatusCode);
    }
}
