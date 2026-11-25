using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace ApisField.Api.Tests;

/// <summary>Testes das fotografias das visitas: envio, leitura, limites e acesso.</summary>
public class PhotoTests
{
    private const int MaxBytes = 2 * 1024 * 1024;

    private static int VisitaDoAndre(ApiFactory f) =>
        f.Query(db => db.Visits.First(v => v.Hive!.Name == "Colmeia do Nabão").Id);

    /// <summary>Bytes que começam como um JPEG (FF D8 FF) e têm o tamanho pedido.</summary>
    private static byte[] Jpeg(int tamanho = 1000)
    {
        var bytes = new byte[tamanho];
        new Random(1).NextBytes(bytes);
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        return bytes;
    }

    private static Task<HttpResponseMessage> EnviarAsync(HttpClient client, int visit, byte[] bytes, string? tipo = "image/jpeg")
    {
        var content = new ByteArrayContent(bytes);
        if (tipo is not null) content.Headers.ContentType = new MediaTypeHeaderValue(tipo);
        return client.PostAsync($"/api/visits/{visit}/photos", content);
    }

    private static async Task<int> EnviarOkAsync(HttpClient client, int visit, byte[] bytes)
    {
        var res = await EnviarAsync(client, visit, bytes);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private static string[] Ficheiros(ApiFactory f) =>
        Directory.Exists(f.PhotosPath) ? Directory.GetFiles(f.PhotosPath) : [];

    [Fact]
    public async Task Enviar_GuardaAFoto_QueSeLeIgual()
    {
        using var f = new ApiFactory();
        var andre = await f.AndreAsync();
        var visit = VisitaDoAndre(f);
        var bytes = Jpeg();

        var id = await EnviarOkAsync(andre, visit, bytes);

        var res = await andre.GetAsync($"/api/photos/{id}");
        Assert.Equal("image/jpeg", res.Content.Headers.ContentType!.MediaType);
        Assert.Equal(bytes, await res.Content.ReadAsByteArrayAsync());
        Assert.Single(Ficheiros(f));

        var visita = await andre.GetFromJsonAsync<JsonElement>($"/api/visits/{visit}");
        Assert.Equal([id], visita.GetProperty("photoIds").EnumerateArray().Select(e => e.GetInt32()));
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData(null)]
    public async Task Enviar_ComOutroTipo_Devolve400(string? tipo)
    {
        using var f = new ApiFactory();
        var res = await EnviarAsync(await f.AndreAsync(), VisitaDoAndre(f), Jpeg(), tipo);
        await AssertFotoInvalida(f, res);
    }

    [Fact]
    public async Task Enviar_ConteudoQueNaoEJpeg_Devolve400()
    {
        // O Content-Type diz JPEG, mas o conteúdo não: não se confia só no cabeçalho.
        using var f = new ApiFactory();
        var res = await EnviarAsync(await f.AndreAsync(), VisitaDoAndre(f), "<html></html>"u8.ToArray());
        await AssertFotoInvalida(f, res);
    }

    [Fact]
    public async Task Enviar_SemConteudo_Devolve400()
    {
        using var f = new ApiFactory();
        await AssertFotoInvalida(f, await EnviarAsync(await f.AndreAsync(), VisitaDoAndre(f), []));
    }

    [Fact]
    public async Task Enviar_Com2MB_EAceite()
    {
        using var f = new ApiFactory();
        await EnviarOkAsync(await f.AndreAsync(), VisitaDoAndre(f), Jpeg(MaxBytes));
    }

    [Fact]
    public async Task Enviar_ComMaisDe2MB_Devolve400()
    {
        using var f = new ApiFactory();
        await AssertFotoInvalida(f, await EnviarAsync(await f.AndreAsync(), VisitaDoAndre(f), Jpeg(MaxBytes + 1)));
    }

    [Fact]
    public async Task Enviar_ASextaFoto_Devolve400()
    {
        using var f = new ApiFactory();
        var andre = await f.AndreAsync();
        var visit = VisitaDoAndre(f);
        for (var i = 0; i < 5; i++) await EnviarOkAsync(andre, visit, Jpeg());

        var res = await EnviarAsync(andre, visit, Jpeg());

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal(5, Ficheiros(f).Length);
    }

    [Fact]
    public async Task Enviar_VariasAoMesmoTempo_NuncaPassaDeCinco()
    {
        using var f = new ApiFactory();
        var andre = await f.AndreAsync();
        var visit = VisitaDoAndre(f);
        for (var i = 0; i < 4; i++) await EnviarOkAsync(andre, visit, Jpeg());

        var respostas = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => EnviarAsync(andre, visit, Jpeg())));

        Assert.Single(respostas, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(7, respostas.Count(r => r.StatusCode == HttpStatusCode.BadRequest));
        Assert.Equal(5, f.Query(db => db.Photos.Count(p => p.VisitId == visit)));
        Assert.Equal(5, Ficheiros(f).Length);
    }

    [Fact]
    public async Task Enviar_SeAGravacaoFalhar_NaoDeixaOFicheiroEmDisco()
    {
        using var f = new ApiFactory();
        var andre = await f.AndreAsync();
        var visit = VisitaDoAndre(f);
        f.Query(db => db.Database.ExecuteSqlRaw(
            "CREATE TRIGGER falha BEFORE INSERT ON Photos BEGIN INSERT INTO NaoExiste VALUES (1); END"));

        var res = await EnviarAsync(andre, visit, Jpeg());

        Assert.Equal(HttpStatusCode.InternalServerError, res.StatusCode);
        Assert.Empty(Ficheiros(f));
    }

    [Fact]
    public async Task Apagar_RemoveORegistoEOFicheiro()
    {
        using var f = new ApiFactory();
        var andre = await f.AndreAsync();
        var id = await EnviarOkAsync(andre, VisitaDoAndre(f), Jpeg());

        Assert.Equal(HttpStatusCode.NoContent, (await andre.DeleteAsync($"/api/photos/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await andre.GetAsync($"/api/photos/{id}")).StatusCode);
        Assert.Empty(Ficheiros(f));
    }

    [Fact]
    public async Task ApagarAVisita_ApagaSoAsFotosDessaVisita()
    {
        using var f = new ApiFactory();
        var andre = await f.AndreAsync();
        var visit = VisitaDoAndre(f);
        var outra = f.Query(db => db.Visits.First(v => v.Hive!.Name == "Alecrim").Id);
        await EnviarOkAsync(andre, visit, Jpeg());
        await EnviarOkAsync(andre, visit, Jpeg());
        var fica = await EnviarOkAsync(andre, outra, Jpeg());

        await andre.DeleteAsync($"/api/visits/{visit}");

        Assert.Equal([fica], f.Query(db => db.Photos.Select(p => p.Id).ToArray()));
        Assert.Single(Ficheiros(f));
    }

    [Fact]
    public async Task FotoDeOutroUtilizador_Devolve404_ENaoMudaNada()
    {
        using var f = new ApiFactory();
        var visit = VisitaDoAndre(f);
        var id = await EnviarOkAsync(await f.AndreAsync(), visit, Jpeg());
        var samuel = await f.SamuelAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await samuel.GetAsync($"/api/photos/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await samuel.DeleteAsync($"/api/photos/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await EnviarAsync(samuel, visit, Jpeg())).StatusCode);

        Assert.Single(Ficheiros(f));
    }

    [Fact]
    public async Task Admin_PodeVerAFotoDeOutroUtilizador()
    {
        using var f = new ApiFactory();
        var id = await EnviarOkAsync(await f.AndreAsync(), VisitaDoAndre(f), Jpeg());

        Assert.Equal(HttpStatusCode.OK, (await (await f.AdminAsync()).GetAsync($"/api/photos/{id}")).StatusCode);
    }

    [Fact]
    public async Task SemToken_Devolve401()
    {
        using var f = new ApiFactory();
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.CreateClient().GetAsync("/api/photos/1")).StatusCode);
    }

    private static async Task AssertFotoInvalida(ApiFactory f, HttpResponseMessage res)
    {
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var errors = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("photo", out _));
        Assert.Empty(Ficheiros(f));
        Assert.Equal(0, f.Query(db => db.Photos.Count()));
    }
}
