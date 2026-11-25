namespace ApisField.Api.Data;

/// <summary>Dados iniciais fictícios, mas plausíveis, da região de Tomar (exemplos da Hive Box).</summary>
/// <remarks>
/// Os utilizadores e as palavras-passe coincidem com a tabela do README, para quem testar a app.
/// As datas são relativas ao momento do seed, para as visitas e os avistamentos parecerem sempre recentes.
/// </remarks>
public static class Seed
{
    /// <summary>Preenche a base de dados, se ainda estiver vazia.</summary>
    /// <remarks>
    /// Idempotente: se já existir algum utilizador, não faz nada. É isto que impede os dados
    /// de se duplicarem a cada reinício da API (coberto por <c>SeedTests</c>).
    /// Grava tudo num único <c>SaveChanges</c>, ou seja, numa só transação: ou fica tudo, ou nada.
    /// </remarks>
    public static void Run(AppDbContext db)
    {
        if (db.Users.Any()) return;

        var now = DateTime.UtcNow;

        // Utilizadores (ver README).
        var admin = new User { Name = "Administrador", Email = "admin@apisfield.pt", PasswordHash = PasswordHasher.Hash("Admin2026!"), Role = "admin" };
        var andre = new User { Name = "André Vassalo", Email = "andre@apisfield.pt", PasswordHash = PasswordHasher.Hash("Abelha2026!") };
        var samuel = new User { Name = "Samuel Ponte", Email = "samuel@apisfield.pt", PasswordHash = PasswordHasher.Hash("Abelha2026!") };
        db.Users.AddRange(admin, andre, samuel);

        // Colmeias: 4 do André, 2 do Samuel. Os valores foram escolhidos para a app mostrar todos os estados:
        // "Sobreiro Velho" em perigo (vespa), "Alecrim" em alerta (CO₂ > 3500 ppm),
        // "Olival de Paialvo" em alerta (bateria < 20 %) e as restantes saudáveis.
        // São valores de referência: a variação simulada (HiveSimulator) nunca os faz atravessar um limiar.
        // Registadas há 90 dias, para o histórico de 7 dias vir sempre completo.
        var created = now.AddDays(-90);
        Hive[] hives =
        [
            new() { Name = "Colmeia do Nabão", Model = "pro", Latitude = 39.6061, Longitude = -8.4127, Label = "Margem do Nabão, Tomar",
                    TempInterior = 34.8, TempExterior = 14.2, Humidity = 61, Weight = 38.4, Co2 = 1450, Battery = 82, Signal = 74, CreatedAt = created, Owner = andre },
            new() { Name = "Sobreiro Velho", Model = "starter", Latitude = 39.5347, Longitude = -8.3995, Label = "Asseiceira",
                    TempInterior = 35.1, TempExterior = 13.8, Humidity = 58, Weight = 41.7, Battery = 67, Signal = 55, WaspDetected = true, CreatedAt = created, Owner = andre },
            new() { Name = "Alecrim", Model = "pro", Latitude = 39.6392, Longitude = -8.3801, Label = "Sabacheira",
                    TempInterior = 34.2, TempExterior = 12.9, Humidity = 66, Weight = 35.9, Co2 = 3820, Battery = 71, Signal = 62, CreatedAt = created, Owner = andre },
            new() { Name = "Olival de Paialvo", Model = "starter", Latitude = 39.5718, Longitude = -8.4689, Label = "Paialvo",
                    TempInterior = 34.6, TempExterior = 14.5, Humidity = 59, Weight = 39.2, Battery = 15, Signal = 48, CreatedAt = created, Owner = andre },
            new() { Name = "Carvalhal", Model = "starter", Latitude = 39.6305, Longitude = -8.3142, Label = "Olalhas",
                    TempInterior = 35.0, TempExterior = 12.4, Humidity = 63, Weight = 36.8, Battery = 90, Signal = 81, CreatedAt = created, Owner = samuel },
            new() { Name = "Rosmaninho", Model = "pro", Latitude = 39.6214, Longitude = -8.4478, Label = "Carregueiros",
                    TempInterior = 34.4, TempExterior = 13.6, Humidity = 60, Weight = 42.3, Co2 = 1980, Battery = 76, Signal = 69, CreatedAt = created, Owner = samuel },
        ];
        db.Hives.AddRange(hives);

        // Visitas: feitas pelo dono da colmeia, às 10h, no próprio local.
        Visit V(Hive h, int daysAgo, bool queen, string brood, string temper, string? treatment, string? notes) => new()
        {
            Hive = h, Date = now.Date.AddDays(-daysAgo).AddHours(10), QueenSeen = queen, BroodLevel = brood, Temperament = temper,
            Treatment = treatment, Notes = notes, Latitude = h.Latitude, Longitude = h.Longitude, Author = h.Owner,
        };
        Visit[] visits =
        [
            V(hives[0], 3, true, "forte", "calma", null, "Quadros de mel quase cheios."),
            V(hives[0], 17, true, "media", "calma", "Ácido oxálico", "Tratamento contra varroa."),
            V(hives[1], 2, false, "media", "nervosa", null, "Vespas asiáticas à entrada. Reduzido o alvado."),
            V(hives[1], 16, true, "forte", "calma", null, null),
            V(hives[2], 5, true, "forte", "calma", null, "Pouca ventilação; abrir a grelha."),
            V(hives[3], 9, false, "fraca", "agressiva", null, "Rainha não vista. Verificar na próxima visita."),
            V(hives[4], 4, true, "media", "calma", "Amitraz", null),
            V(hives[5], 6, true, "forte", "calma", null, "Colocada alça."),
        ];
        db.Visits.AddRange(visits);

        // Avistamentos: concentrados perto da Asseiceira, coerentes com a vespa detetada no "Sobreiro Velho".
        db.Sightings.AddRange(
            new WaspSighting { Type = "nest", Latitude = 39.5362, Longitude = -8.4021, Accuracy = 15, Notes = "Ninho num eucalipto, a cerca de 10 m de altura.", Date = now.AddDays(-2), Author = andre },
            new WaspSighting { Type = "wasp", Latitude = 39.5331, Longitude = -8.3978, Accuracy = 8, Notes = "Várias vespas a atacar a colmeia.", Date = now.AddDays(-2).AddHours(1), Author = andre },
            new WaspSighting { Type = "wasp", Latitude = 39.6042, Longitude = -8.4150, Accuracy = 20, Date = now.AddDays(-5), Author = samuel },
            new WaspSighting { Type = "nest", Latitude = 39.6159, Longitude = -8.3907, Accuracy = 30, Notes = "Ninho primário num beiral.", Date = now.AddDays(-11), Author = samuel },
            new WaspSighting { Type = "wasp", Latitude = 39.5755, Longitude = -8.4612, Accuracy = 12, Date = now.AddDays(-8), Author = admin });
        db.SaveChanges();
    }
}
