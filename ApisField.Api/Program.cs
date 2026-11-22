// Ponto de entrada da ApisField API: regista os serviços, prepara a base de dados e arranca o servidor HTTP.

using ApisField.Api.Auth;
using ApisField.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// ---- Autenticação JWT ----
// A chave vem de "Jwt:Key": em desenvolvimento de appsettings.Development.json, em produção da
// variável de ambiente Jwt__Key. Sem chave, a API recusa-se a arrancar.
var signingKey = TokenService.KeyFrom(builder.Configuration["Jwt:Key"]);
builder.Services.AddSingleton(new TokenService(signingKey));
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        // Mantém os nomes dos claims tal como estão no token ("sub", "role"), sem os converter
        // para os nomes longos do .NET.
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = TokenService.Issuer,
            ValidAudience = TokenService.Audience,
            IssuerSigningKey = signingKey,
            RoleClaimType = "role",
            // Sem tolerância extra: um token expirado é recusado logo.
            ClockSkew = TimeSpan.Zero,
        };
    });

// Todas as rotas exigem token, exceto as marcadas com [AllowAnonymous] (health, registo e login).
// Assim uma rota nova fica protegida por omissão, mesmo que alguém se esqueça do [Authorize].
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// A connection string vem de appsettings.json ("ConnectionStrings:Default").
// Os testes substituem-na por um ficheiro temporário (ver ApiFactory nos testes).
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

// Cria o esquema e os dados iniciais antes de aceitar pedidos.
// Os dois passos são idempotentes: num reinício, com a base de dados já preenchida, não fazem nada.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    Seed.Run(db);
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Fica a servir pedidos até o processo ser terminado (Ctrl+C).
app.Run();
