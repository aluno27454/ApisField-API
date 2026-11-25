using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ApisField.Api.Tests;

/// <summary>Testes das rotas das colmeias: acesso, validação, operações e histórico.</summary>
public class HiveTests
{
    private record LocationDto(double Latitude, double Longitude, string Label);
    private record SensorsDto(double TempInterior, double TempExterior, double Humidity, double Weight, double? Co2,
        int Battery, int Signal, bool WaspDetected);
    private record HiveDto(int Id, string Name, string Model, string Status, LocationDto Location, SensorsDto Sensors,
        DateTime LastUpdate, int OwnerId);
    private record ReadingDto(string Label, DateOnly Date, double Value);
    private record HistoryDto(ReadingDto[] Temperature, ReadingDto[] Weight, ReadingDto[] Co2);

    private static readonly string[] DiasDaSemana = ["Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb"];

    private static int HiveId(ApiFactory f, string name) => f.Query(db => db.Hives.Single(h => h.Name == name).Id);
    private static int UserId(ApiFactory f, string email) => f.Query(db => db.Users.Single(u => u.Email == email).Id);

    /// <summary>Corpo válido de uma colmeia. Cada teste muda só o campo que lhe interessa.</summary>
    private static Dictionary<string, object?> Corpo(string? campo = null, object? valor = null)
    {
        var corpo = new Dictionary<string, object?>
        {
            ["name"] = "Colmeia Nova",
            ["model"] = "pro",
            ["latitude"] = 39.6,
            ["longitude"] = -8.41,
            ["label"] = "Quintal",
        };
        if (campo is not null) corpo[campo] = valor;
        return corpo;
    }

    private static async Task<HiveDto> CriarAsync(HttpClient client, Dictionary<string, object?> corpo)
    {
        var res = await client.PostAsJsonAsync("/api/hives", corpo);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<HiveDto>())!;
    }

    // ---- Listar e ler ----

    [Fact]
    public async Task Lista_SoTrazAsMinhasColmeias_EOAdminVeTodas()
    {
        using var f = new ApiFactory();

        var doAndre = (await (await f.AndreAsync()).GetFromJsonAsync<HiveDto[]>("/api/hives"))!;
        var doSamuel = (await (await f.SamuelAsync()).GetFromJsonAsync<HiveDto[]>("/api/hives"))!;
        var doAdmin = (await (await f.AdminAsync()).GetFromJsonAsync<HiveDto[]>("/api/hives"))!;

        Assert.Equal(4, doAndre.Length);
        Assert.All(doAndre, h => Assert.Equal(UserId(f, "andre@apisfield.pt"), h.OwnerId));
        Assert.Equal(2, doSamuel.Length);
        Assert.Equal(6, doAdmin.Length);
    }

    [Fact]
    public async Task Lista_TemOEstadoESensoresDosDadosIniciais()
    {
        using var f = new ApiFactory();
        var hives = (await (await f.AndreAsync()).GetFromJsonAsync<HiveDto[]>("/api/hives"))!.ToDictionary(h => h.Name);

        Assert.Equal("healthy", hives["Colmeia do Nabão"].Status);
        Assert.Equal("danger", hives["Sobreiro Velho"].Status); // vespa
        Assert.Equal("warning", hives["Alecrim"].Status); // CO₂
        Assert.Equal("warning", hives["Olival de Paialvo"].Status); // bateria

        Assert.True(hives["Sobreiro Velho"].Sensors.WaspDetected);
        Assert.Null(hives["Sobreiro Velho"].Sensors.Co2); // starter
        Assert.NotNull(hives["Colmeia do Nabão"].Sensors.Co2); // pro
        Assert.Equal("Asseiceira", hives["Sobreiro Velho"].Location.Label);
        Assert.InRange(hives["Alecrim"].LastUpdate, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow);
    }

    [Fact]
    public async Task Detalhe_SaiNoFormatoDoContrato()
    {
        using var f = new ApiFactory();
        var json = await (await f.AndreAsync()).GetFromJsonAsync<JsonElement>($"/api/hives/{HiveId(f, "Alecrim")}");

        Assert.Equal(["id", "name", "model", "status", "location", "sensors", "lastUpdate", "ownerId"],
            json.EnumerateObject().Select(p => p.Name));
        Assert.Equal(["latitude", "longitude", "label"], json.GetProperty("location").EnumerateObject().Select(p => p.Name));
        Assert.Equal(["tempInterior", "tempExterior", "humidity", "weight", "co2", "battery", "signal", "waspDetected"],
            json.GetProperty("sensors").EnumerateObject().Select(p => p.Name));
        Assert.EndsWith("Z", json.GetProperty("lastUpdate").GetString());
    }

    [Fact]
    public async Task ColmeiaDeOutroUtilizador_Devolve404EmTodasAsRotas_ENaoMudaNada()
    {
        using var f = new ApiFactory();
        var samuel = await f.SamuelAsync();
        var id = HiveId(f, "Alecrim"); // do André

        Assert.Equal(HttpStatusCode.NotFound, (await samuel.GetAsync($"/api/hives/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await samuel.GetAsync($"/api/hives/{id}/history")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await samuel.PutAsJsonAsync($"/api/hives/{id}", Corpo())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await samuel.DeleteAsync($"/api/hives/{id}")).StatusCode);

        Assert.Equal("Alecrim", f.Query(db => db.Hives.Single(h => h.Id == id).Name));
    }

    [Fact]
    public async Task ColmeiaQueNaoExiste_Devolve404()
    {
        using var f = new ApiFactory();
        Assert.Equal(HttpStatusCode.NotFound, (await (await f.AdminAsync()).GetAsync("/api/hives/9999")).StatusCode);
    }

    [Fact]
    public async Task SemToken_Devolve401()
    {
        using var f = new ApiFactory();
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.CreateClient().GetAsync("/api/hives")).StatusCode);
    }

    // ---- Criar, editar e apagar ----

    [Fact]
    public async Task Criar_Devolve201_ComAColmeiaDoUtilizador()
    {
        using var f = new ApiFactory();
        var andre = await f.AndreAsync();

        var res = await andre.PostAsJsonAsync("/api/hives", Corpo("name", "  Colmeia Nova  "));

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var hive = (await res.Content.ReadFromJsonAsync<HiveDto>())!;
        Assert.Equal($"/api/hives/{hive.Id}", res.Headers.Location!.OriginalString);
        Assert.Equal("Colmeia Nova", hive.Name);
        Assert.Equal(UserId(f, "andre@apisfield.pt"), hive.OwnerId);
        Assert.Equal(new LocationDto(39.6, -8.41, "Quintal"), hive.Location);
        Assert.Equal("healthy", hive.Status);
        Assert.NotNull(hive.Sensors.Co2);
        Assert.Equal(5, (await andre.GetFromJsonAsync<HiveDto[]>("/api/hives"))!.Length);
    }

    [Fact]
    public async Task Criar_Starter_NaoTemCo2_ESemLabelFicaVazia()
    {
        using var f = new ApiFactory();
        var hive = await CriarAsync(await f.SamuelAsync(), Corpo("model", "starter").Where(kv => kv.Key != "label")
            .ToDictionary());

        Assert.Null(hive.Sensors.Co2);
        Assert.Equal("", hive.Location.Label);
    }

    [Fact]
    public async Task Editar_MudaOsDados_MasNaoODono()
    {
        using var f = new ApiFactory();
        var id = HiveId(f, "Colmeia do Nabão"); // pro, do André

        // O admin pode editar as colmeias de todos.
        var res = await (await f.AdminAsync()).PutAsJsonAsync($"/api/hives/{id}", Corpo("model", "starter"));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var hive = (await res.Content.ReadFromJsonAsync<HiveDto>())!;
        Assert.Equal("Colmeia Nova", hive.Name);
        Assert.Equal("starter", hive.Model);
        Assert.Null(hive.Sensors.Co2); // passou a starter
        Assert.Equal(UserId(f, "andre@apisfield.pt"), hive.OwnerId);
    }

    [Fact]
    public async Task Apagar_ApagaTambemAsVisitasAsFotografiasEOsAlertas()
    {
        using var f = new ApiFactory();
        var andre = await f.AndreAsync();
        var id = HiveId(f, "Sobreiro Velho"); // tem visitas e um alerta de vespa
        var outra = HiveId(f, "Alecrim");

        var visita = f.Query(db => db.Visits.First(v => v.HiveId == id).Id);
        var foto = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3]);
        foto.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        Assert.Equal(HttpStatusCode.Created, (await andre.PostAsync($"/api/visits/{visita}/photos", foto)).StatusCode);
        await andre.GetAsync("/api/alerts"); // gera os alertas
        var visitasDaOutra = f.Query(db => db.Visits.Count(v => v.HiveId == outra));

        Assert.Equal(HttpStatusCode.NoContent, (await andre.DeleteAsync($"/api/hives/{id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await andre.GetAsync($"/api/hives/{id}")).StatusCode);
        Assert.Equal(0, f.Query(db => db.Visits.Count(v => v.HiveId == id)));
        Assert.Equal(0, f.Query(db => db.Photos.Count()));
        Assert.Empty(Directory.GetFiles(f.PhotosPath));
        Assert.Equal(0, f.Query(db => db.Alerts.Count(a => a.HiveId == id)));
        Assert.Equal(visitasDaOutra, f.Query(db => db.Visits.Count(v => v.HiveId == outra)));
        Assert.Equal(1, f.Query(db => db.Alerts.Count(a => a.HiveId == outra))); // o CO₂ do Alecrim fica
    }

    // ---- Validação ----

    public static TheoryData<string, object?> Invalidos => new()
    {
        { "name", null },
        { "name", "M" },
        { "name", "   " },
        { "name", new string('a', 61) },
        { "model", null },
        { "model", "mega" },
        { "model", "Pro" }, // os valores do contrato são em minúsculas
        { "latitude", null },
        { "latitude", 90.1 },
        { "latitude", -90.1 },
        { "longitude", null },
        { "longitude", 180.1 },
        { "longitude", -180.1 },
        { "label", new string('a', 101) },
    };

    [Theory]
    [MemberData(nameof(Invalidos))]
    public async Task CampoInvalido_Devolve400ComOErroNesseCampo_AoCriarEAoEditar(string campo, object? valor)
    {
        using var f = new ApiFactory();
        var andre = await f.AndreAsync();

        HttpResponseMessage[] respostas =
        [
            await andre.PostAsJsonAsync("/api/hives", Corpo(campo, valor)),
            await andre.PutAsJsonAsync($"/api/hives/{HiveId(f, "Alecrim")}", Corpo(campo, valor)),
        ];

        foreach (var res in respostas)
        {
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            var errors = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
            Assert.Equal([campo], errors.EnumerateObject().Select(e => e.Name));
        }
        Assert.Equal(6, f.Query(db => db.Hives.Count()));
    }

    [Fact]
    public async Task ValoresNoLimite_SaoAceites()
    {
        using var f = new ApiFactory();
        var andre = await f.AndreAsync();

        await CriarAsync(andre, Corpo("name", new string('a', 60)));
        await CriarAsync(andre, Corpo("name", "Ab"));
        await CriarAsync(andre, Corpo("label", new string('a', 100)));
        var canto = Corpo("latitude", 90.0);
        canto["longitude"] = -180.0;
        await CriarAsync(andre, canto);
    }

    // ---- Histórico ----

    [Fact]
    public async Task Historico_TemSeteDias_DoMaisAntigoParaHoje()
    {
        using var f = new ApiFactory();
        var historico = (await (await f.AndreAsync())
            .GetFromJsonAsync<HistoryDto>($"/api/hives/{HiveId(f, "Alecrim")}/history"))!; // pro

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var dias = Enumerable.Range(-6, 7).Select(hoje.AddDays).ToArray();
        foreach (var serie in new[] { historico.Temperature, historico.Weight, historico.Co2 })
        {
            Assert.Equal(dias, serie.Select(r => r.Date));
            Assert.Equal(dias.Select(d => DiasDaSemana[(int)d.DayOfWeek]), serie.Select(r => r.Label));
            Assert.All(serie, r => Assert.True(r.Value > 0));
        }
        Assert.All(historico.Temperature, r => Assert.InRange(r.Value, 33, 36));
        Assert.All(historico.Co2, r => Assert.True(r.Value > 3500)); // o alerta de CO₂ mantém-se todos os dias
    }

    [Fact]
    public async Task Historico_Starter_TemCo2Vazio()
    {
        using var f = new ApiFactory();
        var historico = (await (await f.AndreAsync())
            .GetFromJsonAsync<HistoryDto>($"/api/hives/{HiveId(f, "Sobreiro Velho")}/history"))!;

        Assert.Empty(historico.Co2);
        Assert.Equal(7, historico.Weight.Length);
    }

    [Fact]
    public async Task Historico_DeUmaColmeiaNova_SoTemDadosDeHoje()
    {
        using var f = new ApiFactory();
        var andre = await f.AndreAsync();
        var hive = await CriarAsync(andre, Corpo());

        var historico = (await andre.GetFromJsonAsync<HistoryDto>($"/api/hives/{hive.Id}/history"))!;

        Assert.All(historico.Temperature[..6], r => Assert.Equal(0, r.Value)); // 0 = sem dados (contrato)
        Assert.True(historico.Temperature[6].Value > 0);
    }
}
