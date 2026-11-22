using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ApisField.Api.Tests;

/// <summary>Testes do registo, do login, de GET /api/me e da proteção das rotas por token.</summary>
public class AuthTests
{
    private record AuthResult(string Token, UserDto User);
    private record UserDto(int Id, string Name, string Email, string Role);

    private static async Task<AuthResult> LoginAsync(HttpClient client, string email, string password)
    {
        var res = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<AuthResult>())!;
    }

    private static HttpRequestMessage MeComToken(string token)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    [Fact]
    public async Task Registo_Devolve201ComToken_QueServeParaOMe()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/auth/register",
            new { name = "Maria Abelha", email = "  Maria@Exemplo.pt ", password = "Mel12345" });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var auth = (await res.Content.ReadFromJsonAsync<AuthResult>())!;
        Assert.Equal("maria@exemplo.pt", auth.User.Email); // email normalizado
        Assert.Equal("user", auth.User.Role);

        var me = await client.SendAsync(MeComToken(auth.Token));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(auth.User.Id, (await me.Content.ReadFromJsonAsync<UserDto>())!.Id);
    }

    [Fact]
    public async Task Registo_ComEmailRepetido_Devolve409()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        // andre@apisfield.pt vem dos dados iniciais (Seed).
        var res = await client.PostAsJsonAsync("/api/auth/register",
            new { name = "Outro André", email = "ANDRE@apisfield.pt", password = "Mel12345" });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Registo_ComDadosInvalidos_Devolve400ComErrosPorCampo()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/auth/register",
            new { name = "M", email = "nao-e-email", password = "12345678" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var errors = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("Name", out _));
        Assert.True(errors.TryGetProperty("Email", out _));
        Assert.True(errors.TryGetProperty("Password", out _)); // só dígitos, sem letras
    }

    [Fact]
    public async Task Login_ComUtilizadorDosDadosIniciais_DevolveToken()
    {
        using var factory = new ApiFactory();
        var auth = await LoginAsync(factory.CreateClient(), "andre@apisfield.pt", "Abelha2026!");

        Assert.False(string.IsNullOrEmpty(auth.Token));
        Assert.Equal("André Vassalo", auth.User.Name);
    }

    [Theory]
    [InlineData("andre@apisfield.pt", "errada123")]
    [InlineData("ninguem@apisfield.pt", "Abelha2026!")]
    public async Task Login_ComCredenciaisErradas_Devolve401(string email, string password)
    {
        using var factory = new ApiFactory();
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Me_SemToken_Devolve401()
    {
        using var factory = new ApiFactory();
        var res = await factory.CreateClient().GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Me_ComTokenAdulterado_Devolve401()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var token = (await LoginAsync(client, "samuel@apisfield.pt", "Abelha2026!")).Token;

        // Troca um carácter da assinatura (a terceira parte do JWT): a verificação HS256 falha.
        var ultimo = token[^1] == 'A' ? 'B' : 'A';
        var adulterado = token[..^1] + ultimo;

        var res = await client.SendAsync(MeComToken(adulterado));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
