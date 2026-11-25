using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ApisField.Api.Tests;

/// <summary>Testes das rotas de visitas: acesso, validação e operações.</summary>
public class VisitTests
{
    private record VisitDto(int Id, int HiveId, string HiveName, DateTime Date, bool QueenSeen, string BroodLevel,
        string Temperament, string? Treatment, string? Notes, double? Latitude, double? Longitude,
        int[] PhotoIds, int AuthorId);

    private static int HiveId(ApiFactory f, string name) => f.Query(db => db.Hives.Single(h => h.Name == name).Id);

    /// <summary>Uma colmeia do André e uma visita a essa colmeia.</summary>
    private static (int hive, int visit) DoAndre(ApiFactory f)
    {
        var hive = HiveId(f, "Colmeia do Nabão");
        return (hive, f.Query(db => db.Visits.First(v => v.HiveId == hive).Id));
    }

    /// <summary>Corpo válido de uma visita. Cada teste muda só o campo que lhe interessa.</summary>
    private static Dictionary<string, object?> Corpo(string? campo = null, object? valor = null)
    {
        var corpo = new Dictionary<string, object?>
        {
            ["date"] = DateTime.UtcNow.AddHours(-1),
            ["queenSeen"] = true,
            ["broodLevel"] = "forte",
            ["temperament"] = "calma",
            ["treatment"] = "Ácido oxálico",
            ["notes"] = "Tudo bem.",
            ["latitude"] = 39.6,
            ["longitude"] = -8.4,
        };
        if (campo is not null) corpo[campo] = valor;
        return corpo;
    }

    // ---- Listar e ler ----

    [Fact]
    public async Task Lista_SoTrazAsVisitasDasMinhasColmeias_DaMaisRecenteParaAMaisAntiga()
    {
        using var f = new ApiFactory();
        var visitas = (await (await f.AndreAsync()).GetFromJsonAsync<VisitDto[]>("/api/visits"))!;

        Assert.Equal(6, visitas.Length); // 6 das 8 visitas iniciais são do André
        Assert.Equal(visitas.OrderByDescending(v => v.Date).Select(v => v.Id), visitas.Select(v => v.Id));
        Assert.Equal("Sobreiro Velho", visitas[0].HiveName);
    }

    [Fact]
    public async Task Lista_DoAdmin_TrazAsDeTodos()
    {
        using var f = new ApiFactory();
        Assert.Equal(8, (await (await f.AdminAsync()).GetFromJsonAsync<VisitDto[]>("/api/visits"))!.Length);
    }

    [Fact]
    public async Task Lista_ComHiveId_FiltraPorColmeia()
    {
        using var f = new ApiFactory();
        var hive = HiveId(f, "Colmeia do Nabão");

        var visitas = (await (await f.AndreAsync()).GetFromJsonAsync<VisitDto[]>($"/api/visits?hiveId={hive}"))!;

        Assert.Equal(2, visitas.Length);
        Assert.All(visitas, v => Assert.Equal(hive, v.HiveId));
    }

    [Fact]
    public async Task Lista_ComHiveIdDeOutroUtilizador_Devolve404()
    {
        using var f = new ApiFactory();
        var res = await (await f.SamuelAsync()).GetAsync($"/api/visits?hiveId={HiveId(f, "Colmeia do Nabão")}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Detalhe_DevolveAVisitaNoFormatoDoContrato()
    {
        using var f = new ApiFactory();
        var (_, visit) = DoAndre(f);

        var json = await (await f.AndreAsync()).GetFromJsonAsync<JsonElement>($"/api/visits/{visit}");

        string[] esperados =
        [
            "id", "hiveId", "hiveName", "date", "queenSeen", "broodLevel", "temperament",
            "treatment", "notes", "latitude", "longitude", "photoIds", "authorId",
        ];
        Assert.Equal(esperados.Order(), json.EnumerateObject().Select(p => p.Name).Order());
        Assert.EndsWith("Z", json.GetProperty("date").GetString());
    }

    // ---- Acesso ----

    [Fact]
    public async Task SemToken_Devolve401()
    {
        using var f = new ApiFactory();
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.CreateClient().GetAsync("/api/visits")).StatusCode);
    }

    [Fact]
    public async Task VisitaOuColmeiaDeOutroUtilizador_Devolve404_ENaoMudaNada()
    {
        using var f = new ApiFactory();
        var samuel = await f.SamuelAsync();
        var (hive, visit) = DoAndre(f);
        var notas = f.Query(db => db.Visits.Single(v => v.Id == visit).Notes);

        Assert.Equal(HttpStatusCode.NotFound, (await samuel.GetAsync($"/api/visits/{visit}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await samuel.PutAsJsonAsync($"/api/visits/{visit}", Corpo())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await samuel.DeleteAsync($"/api/visits/{visit}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await samuel.PostAsJsonAsync($"/api/hives/{hive}/visits", Corpo())).StatusCode);

        Assert.Equal(8, f.Query(db => db.Visits.Count()));
        Assert.Equal(notas, f.Query(db => db.Visits.Single(v => v.Id == visit).Notes));
    }

    [Fact]
    public async Task Admin_PodeEditarAVisitaDeOutroUtilizador_MasOAutorMantemSe()
    {
        using var f = new ApiFactory();
        var (_, visit) = DoAndre(f);
        var autor = f.Query(db => db.Visits.Single(v => v.Id == visit).AuthorId);

        var res = await (await f.AdminAsync()).PutAsJsonAsync($"/api/visits/{visit}", Corpo("notes", "Vista pelo admin."));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = (await res.Content.ReadFromJsonAsync<VisitDto>())!;
        Assert.Equal("Vista pelo admin.", dto.Notes);
        Assert.Equal(autor, dto.AuthorId);
    }

    // ---- Criar, editar e apagar ----

    [Fact]
    public async Task Criar_Devolve201ComAVisitaGravada()
    {
        using var f = new ApiFactory();
        var andre = await f.AndreAsync();
        var (hive, _) = DoAndre(f);

        var res = await andre.PostAsJsonAsync($"/api/hives/{hive}/visits", Corpo());

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var criada = (await res.Content.ReadFromJsonAsync<VisitDto>())!;
        Assert.Equal(hive, criada.HiveId);
        Assert.Equal("Colmeia do Nabão", criada.HiveName);
        Assert.Equal("forte", criada.BroodLevel);
        Assert.Empty(criada.PhotoIds);
        Assert.Equal(f.Query(db => db.Users.Single(u => u.Email == "andre@apisfield.pt").Id), criada.AuthorId);

        // O Location aponta para a visita, e o que se lê é o que se gravou.
        var lida = (await andre.GetFromJsonAsync<VisitDto>(res.Headers.Location))!;
        Assert.Equal(criada.Id, lida.Id);
        Assert.Equal(criada.Date, lida.Date);
    }

    [Fact]
    public async Task Criar_SemOsCamposOpcionais_GravaNull()
    {
        using var f = new ApiFactory();
        var (hive, _) = DoAndre(f);

        var res = await (await f.AndreAsync()).PostAsJsonAsync($"/api/hives/{hive}/visits", new
        {
            date = DateTime.UtcNow, queenSeen = false, broodLevel = "media", temperament = "nervosa", notes = "   ",
        });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var dto = (await res.Content.ReadFromJsonAsync<VisitDto>())!;
        Assert.Null(dto.Treatment);
        Assert.Null(dto.Notes); // só espaços conta como vazio
        Assert.Null(dto.Latitude);
        Assert.Null(dto.Longitude);
    }

    [Theory]
    [InlineData("2026-01-10T09:00:00Z")]
    [InlineData("2026-01-10T10:00:00+01:00")]
    [InlineData("2026-01-10T09:00:00")] // sem fuso: o contrato diz que as datas são UTC
    public async Task Criar_GuardaADataEmUtc(string data)
    {
        using var f = new ApiFactory();
        var (hive, _) = DoAndre(f);

        var res = await (await f.AndreAsync()).PostAsJsonAsync($"/api/hives/{hive}/visits", Corpo("date", data));

        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("2026-01-10T09:00:00Z", json.GetProperty("date").GetString());
    }

    [Fact]
    public async Task Editar_MudaOsCampos_MasNaoAColmeia()
    {
        using var f = new ApiFactory();
        var (hive, visit) = DoAndre(f);

        var res = await (await f.AndreAsync()).PutAsJsonAsync($"/api/visits/{visit}", Corpo("broodLevel", "fraca"));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var gravada = f.Query(db => db.Visits.Single(v => v.Id == visit));
        Assert.Equal("fraca", gravada.BroodLevel);
        Assert.Equal("Tudo bem.", gravada.Notes);
        Assert.Equal(hive, gravada.HiveId);
    }

    [Fact]
    public async Task Apagar_Devolve204_EAVisitaDeixaDeExistir()
    {
        using var f = new ApiFactory();
        var andre = await f.AndreAsync();
        var (_, visit) = DoAndre(f);

        Assert.Equal(HttpStatusCode.NoContent, (await andre.DeleteAsync($"/api/visits/{visit}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await andre.GetAsync($"/api/visits/{visit}")).StatusCode);
    }

    // ---- Validação ----

    public static TheoryData<string, object?> Invalidos => new()
    {
        { "date", null },
        { "queenSeen", null },
        { "broodLevel", null },
        { "broodLevel", "enorme" },
        { "broodLevel", "Forte" }, // os valores do contrato são em minúsculas
        { "temperament", null },
        { "temperament", "feliz" },
        { "treatment", new string('a', 201) },
        { "notes", new string('a', 501) },
        { "latitude", 90.1 },
        { "latitude", -90.1 },
        { "longitude", 180.1 },
        { "longitude", -180.1 },
    };

    [Theory]
    [MemberData(nameof(Invalidos))]
    public async Task CampoInvalido_Devolve400ComOErroNesseCampo_AoCriarEAoEditar(string campo, object? valor)
    {
        using var f = new ApiFactory();
        var andre = await f.AndreAsync();
        var (hive, visit) = DoAndre(f);

        HttpResponseMessage[] respostas =
        [
            await andre.PostAsJsonAsync($"/api/hives/{hive}/visits", Corpo(campo, valor)),
            await andre.PutAsJsonAsync($"/api/visits/{visit}", Corpo(campo, valor)),
        ];

        foreach (var res in respostas)
        {
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            var errors = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
            Assert.Equal([campo], errors.EnumerateObject().Select(e => e.Name));
        }
    }

    [Fact]
    public async Task TamanhosNoLimite_SaoAceites()
    {
        using var f = new ApiFactory();
        var (hive, _) = DoAndre(f);
        var corpo = Corpo("treatment", new string('a', 200));
        corpo["notes"] = new string('a', 500);
        corpo["latitude"] = 90;
        corpo["longitude"] = -180;

        var res = await (await f.AndreAsync()).PostAsJsonAsync($"/api/hives/{hive}/visits", corpo);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Theory]
    [InlineData(4, HttpStatusCode.Created)] // relógio do telemóvel ligeiramente adiantado
    [InlineData(6, HttpStatusCode.BadRequest)]
    public async Task DataNoFuturo_SoAteCincoMinutos(int minutos, HttpStatusCode esperado)
    {
        using var f = new ApiFactory();
        var (hive, _) = DoAndre(f);

        var res = await (await f.AndreAsync()).PostAsJsonAsync($"/api/hives/{hive}/visits",
            Corpo("date", DateTime.UtcNow.AddMinutes(minutos)));

        Assert.Equal(esperado, res.StatusCode);
        if (esperado == HttpStatusCode.BadRequest)
        {
            var errors = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
            Assert.True(errors.TryGetProperty("date", out _));
        }
    }
}
