// Ponto de entrada da ApisField API: regista os serviços, prepara a base de dados e arranca o servidor HTTP.

using ApisField.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

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

app.MapControllers();

// Fica a servir pedidos até o processo ser terminado (Ctrl+C).
app.Run();
